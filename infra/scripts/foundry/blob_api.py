"""
Azure Blob Storage client and upload operations for the Microsoft IQ Solution Accelerator.
"""

import logging
import time
from pathlib import Path

from azure.identity import DefaultAzureCredential

# Module-level logger — inherits configuration from the root logger set up
# by setup_logging() in the entry-point scripts.  No handlers or levels are
# configured here.
logger = logging.getLogger(__name__)

# Data-plane RBAC role assignments can take a few minutes to propagate after
# provisioning, so blob operations are retried to absorb transient
# AuthorizationFailure responses right after deployment.
_MAX_RETRIES = 8
_RETRY_DELAY_SECONDS = 20


def _is_auth_failure(exc: Exception) -> bool:
    """Return True when an exception looks like a transient storage auth failure."""
    text = str(exc)
    return "AuthorizationFailure" in text or "not authorized to perform this operation" in text


def _retry_on_auth_failure(operation, description: str):
    """Run a storage operation, retrying while RBAC role assignments propagate."""
    for attempt in range(1, _MAX_RETRIES + 1):
        try:
            return operation()
        except Exception as exc:  # noqa: BLE001 - retry only on auth propagation
            if not _is_auth_failure(exc) or attempt == _MAX_RETRIES:
                raise
            logger.info(
                f"      Waiting for storage access to propagate ({description}); "
                f"retry {attempt}/{_MAX_RETRIES - 1} in {_RETRY_DELAY_SECONDS}s"
            )
            time.sleep(_RETRY_DELAY_SECONDS)


def create_blob_service_client(blob_endpoint: str):
    """Create an Azure Blob Storage service client.

    Args:
        blob_endpoint: Azure Blob Storage service endpoint URL.

    Returns:
        Authenticated ``BlobServiceClient`` instance.
    """
    from azure.storage.blob import BlobServiceClient

    return BlobServiceClient(blob_endpoint, DefaultAzureCredential())


def upload_pdf_to_blob(
    blob_service_client,
    blob_endpoint: str,
    container_name: str,
    pdf_path,
) -> str:
    """Upload a PDF file to Azure Blob Storage and return its URL.

    Creates the container if it does not already exist.

    Args:
        blob_service_client: Authenticated ``BlobServiceClient``.
        blob_endpoint: Storage service endpoint URL (used to build the returned URL).
        container_name: Target blob container name.
        pdf_path: Path to the PDF file (``str`` or ``Path``).

    Returns:
        Blob URL string for the uploaded file.
    """
    pdf_path = Path(pdf_path)
    container_client = blob_service_client.get_container_client(container_name)
    try:
        _retry_on_auth_failure(
            container_client.create_container, f"create container '{container_name}'"
        )
        logger.debug(f"      Created blob container '{container_name}'")
    except Exception as exc:
        if _is_auth_failure(exc):
            raise
        pass  # container already exists

    with open(pdf_path, "rb") as data:
        blob_client = blob_service_client.get_blob_client(
            container=container_name,
            blob=pdf_path.name,
        )
        _retry_on_auth_failure(
            lambda: blob_client.upload_blob(data, overwrite=True),
            f"upload '{pdf_path.name}'",
        )

    blob_url = f"{blob_endpoint.rstrip('/')}/{container_name}/{pdf_path.name}"
    logger.debug(f"      Uploaded '{pdf_path.name}' to blob storage")
    return blob_url
