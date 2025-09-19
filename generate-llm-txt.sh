#!/bin/bash

# Script to generate llm.txt with repo contents for LLM context
# Excludes .git and docs folders, outputs to docs/llm.txt

set -e

# Check if we're in a git repository
if [ ! -d ".git" ]; then
    echo "Error: Not in a git repository root directory"
    exit 1
fi

# Create docs directory if it doesn't exist
mkdir -p docs

# Output file
OUTPUT_FILE="docs/llm.txt"

# Clear/create the output file
> "$OUTPUT_FILE"

echo "Generating repository overview in $OUTPUT_FILE..."

# Add header
{
    echo "# Repository Structure and Contents"
    echo "Generated on $(date)"
    echo ""
    echo "## Directory Structure"
    echo ""
} >> "$OUTPUT_FILE"

# Generate tree structure excluding .git and docs folders
tree -a -I '.git|docs' >> "$OUTPUT_FILE"

{
    echo ""
    echo "## File Contents"
    echo ""
} >> "$OUTPUT_FILE"

# Find all files excluding .git and docs directories
find . -type f \
    -not -path "./.git/*" \
    -not -path "./docs/*" \
    -not -name "*.exe" \
    -not -name "*.dll" \
    -not -name "*.pdb" \
    -not -name "*.cache" \
    -not -path "./bin/*" \
    -not -path "./obj/*" \
    -not -name "*.png" \
    -not -name "*.jpg" \
    -not -name "*.jpeg" \
    -not -name "*.gif" \
    -not -name "*.ico" \
    -not -name "*.pdf" \
    -not -name "*.zip" \
    -not -name "*.tar.gz" \
    | sort | while read -r file; do
    
    # Skip if file is too large (over 1MB)
    if [ $(stat -f%z "$file" 2>/dev/null || stat -c%s "$file" 2>/dev/null || echo 0) -gt 1048576 ]; then
        {
            echo "### $file"
            echo "*File too large to include (>1MB)*"
            echo ""
        } >> "$OUTPUT_FILE"
        continue
    fi
    
    # Check if file is likely text/readable
    if file "$file" | grep -qE "(text|ASCII|UTF-8|JSON|XML|empty)"; then
        {
            echo "### $file"
            echo '```'
            cat "$file"
            echo '```'
            echo ""
        } >> "$OUTPUT_FILE"
    else
        {
            echo "### $file"
            echo "*Binary file - content not included*"
            echo ""
        } >> "$OUTPUT_FILE"
    fi
done

echo "Repository overview generated successfully in $OUTPUT_FILE"
echo "File size: $(du -h "$OUTPUT_FILE" | cut -f1)"
