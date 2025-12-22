@echo off
"C:\Program Files\Git\cmd\git.exe" add -A
"C:\Program Files\Git\cmd\git.exe" commit -m "feat(config): add column order persistence to AppConfig" -m "- Added LibraryColumnOrder property for saving user preferences" -m "- Stores comma-separated column IDs" -m "- Empty string defaults to hardcoded layout"
