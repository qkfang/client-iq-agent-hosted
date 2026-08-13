"""PDF creation tool for the hosted agent session."""

import base64
import os
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Annotated
from xml.sax.saxutils import escape

from agent_framework import tool
from reportlab.lib import colors
from reportlab.lib.pagesizes import LETTER
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import KeepTogether, ListFlowable, ListItem, Paragraph, SimpleDocTemplate, Spacer

_MAX_CONTENT_LENGTH = 100_000
_MIN_CONTENT_LENGTH = 40

# Attachment bytes are returned to the model, so keep the payload small.
_MAX_ATTACHMENT_BYTES = 100_000

_BULLET_PATTERN = re.compile(r"^([-*+]|\d+[.)])\s+(.*)$")
_HEADING_PATTERN = re.compile(r"^(#{1,3})\s+(.*)$")


def _safe_file_name(file_name: str) -> str:
    stem = Path(file_name).stem
    stem = re.sub(r"[^A-Za-z0-9._-]+", "-", stem).strip(".-_")
    return f"{stem[:80] or 'document'}.pdf"


def _inline(text: str) -> str:
    """Escape XML then map lightweight markdown to ReportLab inline markup."""
    marked = escape(text.strip())
    marked = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", marked)
    marked = re.sub(r"(?<!\w)_(.+?)_(?!\w)", r"<i>\1</i>", marked)
    marked = re.sub(r"`(.+?)`", r'<font face="Courier">\1</font>', marked)
    return marked


def _build_story(content: str, styles) -> list:
    """Convert plain/markdown-ish text into flowables that render on the page."""
    story: list = []
    pending_items: list[str] = []
    ordered = False

    def flush_items() -> None:
        if not pending_items:
            return
        story.append(
            ListFlowable(
                [ListItem(Paragraph(item, styles["BodyText"]), leftIndent=18) for item in pending_items],
                bulletType="1" if ordered else "bullet",
                start=1 if ordered else "\u2022",
                leftIndent=18,
            )
        )
        story.append(Spacer(1, 0.1 * inch))
        pending_items.clear()

    for raw_line in content.strip().splitlines():
        line = raw_line.strip()
        if not line:
            flush_items()
            continue

        heading = _HEADING_PATTERN.match(line)
        if heading:
            flush_items()
            level = min(len(heading.group(1)), 3)
            story.append(
                KeepTogether([Paragraph(_inline(heading.group(2)), styles[f"Heading{level}"]), Spacer(1, 0.06 * inch)])
            )
            continue

        bullet = _BULLET_PATTERN.match(line)
        if bullet:
            is_ordered = bullet.group(1)[0].isdigit()
            if pending_items and is_ordered != ordered:
                flush_items()
            ordered = is_ordered
            pending_items.append(_inline(bullet.group(2)))
            continue

        flush_items()
        story.append(Paragraph(_inline(line), styles["BodyText"]))
        story.append(Spacer(1, 0.08 * inch))

    flush_items()
    return story


@tool
def create_pdf(
    title: Annotated[str, "Title displayed at the top of the PDF"],
    content: Annotated[
        str,
        "Full document body to render on the page. Supports '#'/'##'/'###' headings, "
        "'-' or '1.' list items, blank-line paragraph breaks, and **bold**, _italic_, `code` markup.",
    ],
    file_name: Annotated[str, "Requested PDF filename, with or without a .pdf extension"] = "document.pdf",
    subject: Annotated[str, "Optional PDF subject metadata"] = "",
    author: Annotated[str, "Optional PDF author metadata"] = "",
) -> dict[str, str | int]:
    """Create a PDF in the current hosted session for the user to download."""
    clean_title = title.strip()
    clean_content = content.strip()
    if not clean_title:
        raise ValueError("A PDF title is required.")
    if not clean_content:
        raise ValueError("PDF content cannot be empty.")
    if len(clean_content) < _MIN_CONTENT_LENGTH:
        raise ValueError(
            f"PDF content is only {len(clean_content)} characters. Pass the full document body "
            f"(at least {_MIN_CONTENT_LENGTH} characters), not a summary or placeholder."
        )
    if len(clean_content) > _MAX_CONTENT_LENGTH:
        raise ValueError(f"PDF content cannot exceed {_MAX_CONTENT_LENGTH} characters.")

    output_name = _safe_file_name(file_name)
    output_path = Path(os.environ.get("HOME", os.getcwd())) / output_name
    styles = getSampleStyleSheet()
    styles.add(ParagraphStyle(name="DocSubtitle", parent=styles["Normal"], fontSize=9, textColor=colors.HexColor("#555555")))
    document = SimpleDocTemplate(
        str(output_path),
        pagesize=LETTER,
        rightMargin=0.75 * inch,
        leftMargin=0.75 * inch,
        topMargin=0.75 * inch,
        bottomMargin=0.75 * inch,
        title=clean_title,
        subject=subject.strip() or clean_title,
        author=author.strip() or "Operations Assistant",
    )

    generated_on = datetime.now(timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    story = [
        Paragraph(escape(clean_title), styles["Title"]),
        Paragraph(f"Generated {generated_on}", styles["DocSubtitle"]),
        Spacer(1, 0.25 * inch),
    ]
    body = _build_story(clean_content, styles)
    if not body:
        raise ValueError("PDF content produced no renderable text.")
    story.extend(body)

    def _footer(canvas, doc) -> None:
        canvas.saveState()
        canvas.setFont("Helvetica", 8)
        canvas.drawString(0.75 * inch, 0.5 * inch, clean_title[:80])
        canvas.drawRightString(LETTER[0] - 0.75 * inch, 0.5 * inch, f"Page {doc.page}")
        canvas.restoreState()

    document.build(story, onFirstPage=_footer, onLaterPages=_footer)
    return {
        "status": "created",
        "file_name": output_name,
        "file_path": output_name,
        "media_type": "application/pdf",
        "size_bytes": output_path.stat().st_size,
        "page_count": document.page,
        "content_chars": len(clean_content),
    }


@tool
def get_pdf_attachment(
    file_name: Annotated[str, "Name of a PDF already created in this session"],
) -> dict[str, str]:
    """Return a session PDF as a mail attachment payload for sending email."""
    output_name = _safe_file_name(file_name)
    output_path = Path(os.environ.get("HOME", os.getcwd())) / output_name
    if not output_path.is_file():
        raise FileNotFoundError(f"'{output_name}' was not found in this session.")

    data = output_path.read_bytes()
    if len(data) > _MAX_ATTACHMENT_BYTES:
        raise ValueError(
            f"'{output_name}' is {len(data)} bytes, which exceeds the "
            f"{_MAX_ATTACHMENT_BYTES} byte limit for email attachments."
        )

    return {
        "@odata.type": "#microsoft.graph.fileAttachment",
        "name": output_name,
        "contentType": "application/pdf",
        "contentBytes": base64.b64encode(data).decode("ascii"),
    }