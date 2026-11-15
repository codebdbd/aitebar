#!/usr/bin/env bash
set -euo pipefail

DATA_DIR="${1:-/var/lib/telegram-post-editor}"
BACKUP_DIR="${2:-/var/backups/telegram-post-editor}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
TARGET_DIR="${BACKUP_DIR}/${STAMP}"

mkdir -p "${TARGET_DIR}"

# Consistent SQLite online backup
sqlite3 "${DATA_DIR}/server.db" ".backup '${TARGET_DIR}/server.db'"
if [ -d "${DATA_DIR}/media" ]; then
  cp -a "${DATA_DIR}/media" "${TARGET_DIR}/media"
fi

echo "Backup completed: ${TARGET_DIR}"
