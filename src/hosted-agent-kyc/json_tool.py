"""JSON report creation tool for the hosted agent session."""

import base64
import json
import os
import re
from pathlib import Path
from typing import Annotated

from agent_framework import tool

# Attachment bytes are returned to the model, so keep the payload small.
_MAX_ATTACHMENT_BYTES = 100_000


def _safe_file_name(file_name: str) -> str:
    stem = Path(file_name).stem
    stem = re.sub(r"[^A-Za-z0-9._-]+", "-", stem).strip(".-_")
    return f"{stem[:80] or 'document'}.json"


@tool
def create_json_report(
    payload: Annotated[dict, "The compliance case JSON payload to save, e.g. the build_compliance_response result"],
    file_name: Annotated[str, "Requested JSON filename, with or without a .json extension"] = "document.json",
) -> dict[str, str | int]:
    """Save a JSON payload as a standalone file in the current hosted session for the user to download."""
    if not payload:
        raise ValueError("JSON payload cannot be empty.")

    output_name = _safe_file_name(file_name)
    output_path = Path(os.environ.get("HOME", os.getcwd())) / output_name
    output_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    return {
        "status": "created",
        "file_name": output_name,
        "file_path": output_name,
        "media_type": "application/json",
        "size_bytes": output_path.stat().st_size,
    }


@tool
def get_json_attachment(
    file_name: Annotated[str, "Name of a JSON file already created in this session"],
) -> dict[str, str]:
    """Return a session JSON file as a mail attachment payload for sending email."""
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
        "contentType": "application/json",
        "contentBytes": base64.b64encode(data).decode("ascii"),
    }
