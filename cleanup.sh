#!/usr/bin/env bash
# WebCartographer Repository Cleanup Script
# Safe cleanup with backups and validation

set -e  # Exit on error

echo "════════════════════════════════════════════════════════════"
echo "🧹 WebCartographer Repository Cleanup"
echo "════════════════════════════════════════════════════════════"
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

# Safety check
if [ ! -f "WebCartographer.sln" ]; then
    echo -e "${RED}❌ Error: Must run from WebCartographer root directory${NC}"
    exit 1
fi

echo -e "${YELLOW}⚠️  This script will:${NC}"
echo "  1. Archive 13 development documentation files"
echo "  2. Delete 1 outdated file"
echo "  3. Remove unused DataCollector.cs"
echo "  4. Remove duplicate tarball"
echo "  5. Update .gitignore for build artifacts"
echo ""
echo -e "${YELLOW}📝 A backup branch 'cleanup-backup' will be created${NC}"
echo ""
read -p "Continue? (y/N) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Cancelled."
    exit 0
fi

echo ""
echo "════════════════════════════════════════════════════════════"
echo "Step 1: Create backup branch"
echo "════════════════════════════════════════════════════════════"

if git rev-parse --verify cleanup-backup >/dev/null 2>&1; then
    echo -e "${YELLOW}⚠️  Branch 'cleanup-backup' already exists, skipping${NC}"
else
    git branch cleanup-backup
    echo -e "${GREEN}✅ Created backup branch 'cleanup-backup'${NC}"
fi

echo ""
echo "════════════════════════════════════════════════════════════"
echo "Step 2: Create docs/archive directory"
echo "════════════════════════════════════════════════════════════"

mkdir -p docs/archive
echo -e "${GREEN}✅ Created docs/archive/${NC}"

echo ""
echo "════════════════════════════════════════════════════════════"
echo "Step 3: Archive development documentation"
echo "════════════════════════════════════════════════════════════"

# List of files to archive
archive_files=(
    "BUILD-SUCCESS.md"
    "HISTORICAL-TRACKING-COMPLETE.md"
    "INTEGRATED-SERVER-SUMMARY.md"
    "INTEGRATION-COMPLETE.md"
    "INTEGRATION-SUMMARY.md"
    "PHASE-1-IMPROVEMENTS.md"
    "SERVER-ONLY-MOD.md"
    "SERVERSTATUSQUERY-IMPROVEMENTS.md"
    "SETUP-LIVE-SERVER.md"
    "UNIFIED-MOD-GUIDE.md"
    "UNIFIED-MOD-INTEGRATION-GUIDE.md"
    "WEBCARTOGRAPHER-LIVE-SETUP.md"
    "IMPROVEMENT_PLAN.md"
)

for file in "${archive_files[@]}"; do
    if [ -f "$file" ]; then
        mv "$file" docs/archive/
        echo -e "${GREEN}✅ Archived: $file${NC}"
    else
        echo -e "${YELLOW}⚠️  Not found (skipping): $file${NC}"
    fi
done

echo ""
echo "════════════════════════════════════════════════════════════"
echo "Step 4: Delete outdated documentation"
echo "════════════════════════════════════════════════════════════"

if [ -f "QUICK-IMPROVEMENTS-REFERENCE.md" ]; then
    rm -f "QUICK-IMPROVEMENTS-REFERENCE.md"
    echo -e "${GREEN}✅ Deleted: QUICK-IMPROVEMENTS-REFERENCE.md${NC}"
fi

echo ""
echo "════════════════════════════════════════════════════════════"
echo "Step 5: Remove unused code files"
echo "════════════════════════════════════════════════════════════"

if [ -f "WebCartographer/Services/DataCollector.cs" ]; then
    rm -f "WebCartographer/Services/DataCollector.cs"
    echo -e "${GREEN}✅ Deleted: WebCartographer/Services/DataCollector.cs${NC}"
else
    echo -e "${YELLOW}⚠️  File not found: DataCollector.cs${NC}"
fi

echo ""
echo "════════════════════════════════════════════════════════════"
echo "Step 6: Remove duplicate tarballs"
echo "════════════════════════════════════════════════════════════"

if [ -f "WebCartographer/WebCartographer-v2.0.0.tar.gz" ]; then
    rm -f "WebCartographer/WebCartographer-v2.0.0.tar.gz"
    echo -e "${GREEN}✅ Deleted: WebCartographer/WebCartographer-v2.0.0.tar.gz${NC}"
fi

echo ""
echo "════════════════════════════════════════════════════════════"
echo "Step 7: Update .gitignore"
echo "════════════════════════════════════════════════════════════"

if [ ! -f ".gitignore" ]; then
    echo "# Creating .gitignore"
    touch .gitignore
fi

# Add build artifacts to .gitignore if not already present
cat >> .gitignore << 'EOF'

# Build outputs (added by cleanup script)
**/bin/
**/obj/
**/*.user
**/*.suo
**/*.cache

# Release packages in subdirectories
WebCartographer/WebCartographer-*.tar.gz
WebCartographer/WebCartographer-*.zip

# IDE files
.vs/
.idea/
.vscode/
*.DotSettings.user
.DS_Store
Thumbs.db

# Nix
result
result-*
EOF

echo -e "${GREEN}✅ Updated .gitignore${NC}"

echo ""
echo "════════════════════════════════════════════════════════════"
echo "Step 8: Remove build artifacts from git (if tracked)"
echo "════════════════════════════════════════════════════════════"

# Remove from git but keep locally
git rm -r --cached WebCartographer/bin/ 2>/dev/null && echo -e "${GREEN}✅ Removed WebCartographer/bin/ from git${NC}" || echo -e "${YELLOW}⚠️  WebCartographer/bin/ not tracked${NC}"
git rm -r --cached WebCartographer/obj/ 2>/dev/null && echo -e "${GREEN}✅ Removed WebCartographer/obj/ from git${NC}" || echo -e "${YELLOW}⚠️  WebCartographer/obj/ not tracked${NC}"
git rm -r --cached WebCartographer.Tests/bin/ 2>/dev/null && echo -e "${GREEN}✅ Removed WebCartographer.Tests/bin/ from git${NC}" || echo -e "${YELLOW}⚠️  WebCartographer.Tests/bin/ not tracked${NC}"
git rm -r --cached WebCartographer.Tests/obj/ 2>/dev/null && echo -e "${GREEN}✅ Removed WebCartographer.Tests/obj/ from git${NC}" || echo -e "${YELLOW}⚠️  WebCartographer.Tests/obj/ not tracked${NC}"
git rm --cached *.DotSettings.user 2>/dev/null && echo -e "${GREEN}✅ Removed .DotSettings.user files${NC}" || echo -e "${YELLOW}⚠️  No .DotSettings.user files tracked${NC}"

echo ""
echo "════════════════════════════════════════════════════════════"
echo "Step 9: Create archive README"
echo "════════════════════════════════════════════════════════════"

cat > docs/archive/README.md << 'EOF'
# WebCartographer Development Archive

This directory contains historical documentation from the development process.

## What's Here

These documents chronicle the development journey from conception to v2.0.0:

- **Integration guides** - How the unified mod was created
- **Development milestones** - Completion markers for major features
- **Setup guides** - Old installation instructions (superseded by main docs)
- **Improvement plans** - Original feature roadmaps

## Current Documentation

For current, up-to-date documentation, see:

- `../../README.md` - Project overview
- `../../QUICK-START.md` - Installation guide
- `../../DEPLOYMENT-GUIDE.md` - Full deployment docs
- `../../PERFORMANCE-OPTIMIZATION.md` - Tuning guide

## Purpose

These files are preserved for:
1. **Historical reference** - Understanding design decisions
2. **Developer context** - How we got here
3. **Future improvements** - Ideas that weren't implemented yet

**Last Updated:** October 1, 2025
EOF

echo -e "${GREEN}✅ Created docs/archive/README.md${NC}"

echo ""
echo "════════════════════════════════════════════════════════════"
echo "Step 10: Verify build still works"
echo "════════════════════════════════════════════════════════════"

echo "Testing build..."
if command -v nix &> /dev/null; then
    if nix develop --command bash -c "cd WebCartographer && dotnet build -c Release" > /dev/null 2>&1; then
        echo -e "${GREEN}✅ Build successful!${NC}"
    else
        echo -e "${RED}❌ Build failed! Check errors above${NC}"
        echo -e "${YELLOW}⚠️  Restore from backup: git checkout cleanup-backup${NC}"
        exit 1
    fi
else
    echo -e "${YELLOW}⚠️  Nix not available, skipping build test${NC}"
    echo "   Run manually: cd WebCartographer && dotnet build"
fi

echo ""
echo "════════════════════════════════════════════════════════════"
echo "✅ Cleanup Complete!"
echo "════════════════════════════════════════════════════════════"
echo ""
echo -e "${GREEN}Summary:${NC}"
echo "  • Archived 13 development docs to docs/archive/"
echo "  • Deleted 1 outdated file"
echo "  • Removed unused code"
echo "  • Updated .gitignore"
echo "  • Removed build artifacts from git"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "  1. Review changes: git status"
echo "  2. Check git diff: git diff"
echo "  3. Commit changes: git add -A && git commit -m 'chore: cleanup repository structure'"
echo "  4. If issues arise: git checkout cleanup-backup"
echo ""
echo -e "${GREEN}Happy coding! 🚀${NC}"

