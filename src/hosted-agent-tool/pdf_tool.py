"""PDF creation tool for the hosted agent session."""

import base64
import os
import re
from pathlib import Path
from typing import Annotated
from xml.sax.saxutils import escape

from agent_framework import tool
from reportlab.lib.pagesizes import LETTER
from reportlab.lib.styles import getSampleStyleSheet
from reportlab.lib.units import inch
from reportlab.platypus import Paragraph, SimpleDocTemplate, Spacer

_MAX_CONTENT_LENGTH = 100_000

# Attachment bytes are returned to the model, so keep the payload small.
_MAX_ATTACHMENT_BYTES = 100_000


def _safe_file_name(file_name: str) -> str:
    stem = Path(file_name).stem
    stem = re.sub(r"[^A-Za-z0-9._-]+", "-", stem).strip(".-_")
    return f"{stem[:80] or 'document'}.pdf"


@tool
def create_pdf(
    title: Annotated[str, "Title displayed at the top of the PDF"],
    content: Annotated[str, "Plain-text content to include in the PDF"],
    file_name: Annotated[str, "Requested PDF filename, with or without a .pdf extension"] = "document.pdf",
) -> dict[str, str | int]:
    """Create a PDF in the current hosted session for the user to download."""
    if not title.strip():
        raise ValueError("A PDF title is required.")
    if not content.strip():
        raise ValueError("PDF content cannot be empty.")
    if len(content) > _MAX_CONTENT_LENGTH:
        raise ValueError(f"PDF content cannot exceed {_MAX_CONTENT_LENGTH} characters.")

    output_name = _safe_file_name(file_name)
    output_path = Path(os.environ.get("HOME", os.getcwd())) / output_name
    styles = getSampleStyleSheet()
    document = SimpleDocTemplate(
        str(output_path),
        pagesize=LETTER,
        rightMargin=0.75 * inch,
        leftMargin=0.75 * inch,
        topMargin=0.75 * inch,
        bottomMargin=0.75 * inch,
        title=title.strip(),
    )

    story = [Paragraph(escape(title.strip()), styles["Title"]), Spacer(1, 0.25 * inch)]
    for block in re.split(r"\n\s*\n", content.strip()):
        paragraph = escape(block.strip()).replace("\n", "<br/>")
        if paragraph:
            story.extend((Paragraph(paragraph, styles["BodyText"]), Spacer(1, 0.12 * inch)))

    document.build(story)
    return {
        "status": "created",
        "file_name": output_name,
        "file_path": output_name,
        "media_type": "application/pdf",
        "size_bytes": output_path.stat().st_size,
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