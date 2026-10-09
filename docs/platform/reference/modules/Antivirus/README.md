# Antivirus (`Crest.Antivirus`)

The Antivirus module provides features that inspect uploaded files before Crest stores or imports them permanently.

## ClamAV feature (`Crest.Antivirus.ClamAV`)

The ClamAV feature scans files with a `clamd` service before Crest stores or imports them.

When the feature is enabled, uploads fail:

- Malware detections are rejected before storage.
- Scanner connectivity, timeout, or protocol failures also reject the upload.
- The ClamAV connection is reused per configuration to avoid creating a new TCP client for every scan.

The scanner is wired through Crest's file event handling abstractions, so uploads can be validated before storage without coupling media and deployment flows to a scanner-specific interface. ClamAV participates in that flow as an `IFileEventHandler`, and `FileCreationService` aborts the upload when ClamAV returns a failed `FileCreatingResult`.

### Configuration

Configure the ClamAV connection in application configuration. The settings key remains `Crest_Antivirus_ClamAV` for compatibility:

```json
{
  "Crest": {
    "Crest_Antivirus_ClamAV": {
      "Host": "localhost",
      "Port": 3310,
      "ConnectTimeoutSeconds": 5,
      "TransferTimeoutSeconds": 30
    }
  }
}
```

The same settings can be provided with environment variables:

```text
Crest__Antivirus_ClamAV__Host=localhost
Crest__Antivirus_ClamAV__Port=3310
Crest__Antivirus_ClamAV__ConnectTimeoutSeconds=5
Crest__Antivirus_ClamAV__TransferTimeoutSeconds=30
```

### Usage

1. Configure the ClamAV settings.
2. Enable the `ClamAV Antivirus Scanner` feature (`Crest.Antivirus.ClamAV`).
3. Ensure a reachable `clamd` instance is running.

If the feature is enabled without a valid ClamAV connection, uploads are rejected until the scanner can verify them.

This feature integrates with the shared file upload security pipeline through `IFileEventHandler`, so uploads can be rejected before Crest stores them permanently.

See [File Upload Security](../../core/file-upload-security.md) for the canonical guidance on invoking `FileCreationService` in custom upload flows and aborting rejected files before they are stored.

### Notes

- The currently audited upload surfaces covered by this change are media uploads plus deployment package imports, both local and remote.
- Media uploads are scanned before storage because they flow through `DefaultMediaFileStore`.
- Deployment package zip/json uploads are scanned before Crest writes the uploaded file to a temporary archive location for import.
