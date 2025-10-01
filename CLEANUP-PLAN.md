# 🧹 WebCartographer Repository Cleanup Plan

## 📊 Current Status

**Issues Found:**

- 🔴 **25 markdown files** (many redundant from development)
- 🔴 **Build artifacts** in bin/ and obj/ (3.7 MB)
- 🔴 **Unused code files** (old DataCollector, unused services)
- 🔴 **Empty test project** (placeholder tests only)
- 🔴 **Duplicate tarballs** (in root and WebCartographer/)
- 🟡 **Old build system** (Cake build files)

---

## 🎯 Cleanup Actions

### **1. Documentation Consolidation** (Priority: HIGH)

**Problem:** 25 markdown files, many redundant

**Keep (Essential 5):**

- ✅ `README.md` - Project overview
- ✅ `QUICK-START.md` - Installation guide
- ✅ `DEPLOYMENT-GUIDE.md` - Full deployment docs
- ✅ `PERFORMANCE-OPTIMIZATION.md` - Tuning guide
- ✅ `LICENSE` - Legal requirement

**Archive (Move to `docs/archive/`):**

- 📦 `BUILD-SUCCESS.md` - Development log
- 📦 `HISTORICAL-TRACKING-COMPLETE.md` - Dev milestone
- 📦 `INTEGRATED-SERVER-SUMMARY.md` - Integration notes
- 📦 `INTEGRATION-COMPLETE.md` - Dev milestone
- 📦 `INTEGRATION-SUMMARY.md` - Integration notes
- 📦 `PHASE-1-IMPROVEMENTS.md` - Phase log
- 📦 `SERVER-ONLY-MOD.md` - Dev notes
- 📦 `SERVERSTATUSQUERY-IMPROVEMENTS.md` - Dev notes
- 📦 `SETUP-LIVE-SERVER.md` - Old setup guide
- 📦 `UNIFIED-MOD-GUIDE.md` - Integration guide
- 📦 `UNIFIED-MOD-INTEGRATION-GUIDE.md` - Integration guide
- 📦 `WEBCARTOGRAPHER-LIVE-SETUP.md` - Old setup guide
- 📦 `IMPROVEMENT_PLAN.md` - Original plan (now complete)

**Merge Into Main Docs:**

- 🔄 `HTTP-SERVER-EXPLANATION.md` → Merge into README.md
- 🔄 `NGINX-COMPATIBILITY.md` → Merge into DEPLOYMENT-GUIDE.md
- 🔄 `NIX-DEVELOPMENT.md` → Keep separate or merge into README
- 🔄 `PERFORMANCE-FIXES-APPLIED.md` → Merge into PERFORMANCE-OPTIMIZATION.md
- 🔄 `QUICK-IMPROVEMENTS-REFERENCE.md` → Delete (outdated)
- 🔄 `PORT-80-SETUP.md` → Merge into DEPLOYMENT-GUIDE.md
- 🔄 `TESTING-GUIDE.md` → Keep separate or merge into DEPLOYMENT-GUIDE
- 🔄 `HISTORICAL-TRACKING-GUIDE.md` → Merge into DEPLOYMENT-GUIDE.md or README

**Result:** 25 files → 5-7 essential docs

---

### **2. Code Cleanup** (Priority: MEDIUM)

**Remove Unused Code:**

```
WebCartographer/Services/DataCollector.cs
```

- **Why:** Replaced by `DataCollectorImproved.cs`
- **Action:** Delete

**Optional to Keep:**

```
WebCartographer/Services/HistoricalTrackerOptimized.cs
```

- **Why:** Alternative async implementation
- **Action:** Keep for now, document in README

**Build System:**

```
build/Build.csproj
build/build.ps1
build/build.sh
build/CakeBuilder.cs
```

- **Why:** Cake build system, but we use `dotnet build` directly
- **Action:** Keep for compatibility, but not required

---

### **3. Test Project Cleanup** (Priority: LOW)

**Problem:** Empty/placeholder tests

```
WebCartographer.Tests/
├── UnitTest1.cs (placeholder)
├── Usings.cs
└── WebCartographer.Tests.csproj
```

**Options:**

1. **Delete entirely** (no tests written yet)
2. **Keep structure** for future tests
3. **Write actual tests** (time investment)

**Recommendation:** Keep structure, add TODO

---

### **4. Build Artifacts** (Priority: HIGH)

**Problem:** Build artifacts in git (3.7 MB)

**Add to `.gitignore`:**

```gitignore
# Build outputs
**/bin/
**/obj/
**/*.user
**/*.suo
**/*.cache

# Release packages (keep only in root)
WebCartographer/WebCartographer-*.tar.gz

# IDE files
.vs/
.idea/
.vscode/
*.DotSettings.user

# OS files
.DS_Store
Thumbs.db
```

**Then remove from git:**

```bash
git rm -r --cached WebCartographer/bin/
git rm -r --cached WebCartographer/obj/
git rm -r --cached WebCartographer.Tests/bin/
git rm -r --cached WebCartographer.Tests/obj/
git rm --cached WebCartographer/WebCartographer-v2.0.0.tar.gz
git rm --cached *.DotSettings.user
```

---

### **5. Project Structure** (Priority: MEDIUM)

**Current:**

```
WebCartographer/
├── WebCartographer/ (mod)
├── WebCartographer.Tests/
├── WebCartographerColorExporter/ (separate mod)
├── WebCartographerSync/ (deleted but dir might remain)
└── build/
```

**Recommendation:**

- Keep `WebCartographerColorExporter/` as separate client-side mod
- Remove any leftover `WebCartographerSync/` references
- Document separation in README

---

## 📝 Cleanup Script

```bash
#!/bin/bash
set -e

echo "🧹 Starting WebCartographer cleanup..."

# 1. Create archive directory
mkdir -p docs/archive

# 2. Archive development docs
mv BUILD-SUCCESS.md docs/archive/
mv HISTORICAL-TRACKING-COMPLETE.md docs/archive/
mv INTEGRATED-SERVER-SUMMARY.md docs/archive/
mv INTEGRATION-COMPLETE.md docs/archive/
mv INTEGRATION-SUMMARY.md docs/archive/
mv PHASE-1-IMPROVEMENTS.md docs/archive/
mv SERVER-ONLY-MOD.md docs/archive/
mv SERVERSTATUSQUERY-IMPROVEMENTS.md docs/archive/
mv SETUP-LIVE-SERVER.md docs/archive/
mv UNIFIED-MOD-GUIDE.md docs/archive/
mv UNIFIED-MOD-INTEGRATION-GUIDE.md docs/archive/
mv WEBCARTOGRAPHER-LIVE-SETUP.md docs/archive/
mv IMPROVEMENT_PLAN.md docs/archive/

# 3. Delete outdated docs
rm -f QUICK-IMPROVEMENTS-REFERENCE.md

# 4. Remove unused code
rm -f WebCartographer/Services/DataCollector.cs

# 5. Remove duplicate tarball
rm -f WebCartographer/WebCartographer-v2.0.0.tar.gz

# 6. Clean build artifacts (after adding to .gitignore)
git rm -r --cached WebCartographer/bin/ 2>/dev/null || true
git rm -r --cached WebCartographer/obj/ 2>/dev/null || true
git rm -r --cached WebCartographer.Tests/bin/ 2>/dev/null || true
git rm -r --cached WebCartographer.Tests/obj/ 2>/dev/null || true
git rm --cached *.DotSettings.user 2>/dev/null || true

echo "✅ Cleanup complete!"
echo ""
echo "Next steps:"
echo "1. Review changes with: git status"
echo "2. Update README.md with merged content"
echo "3. Commit: git commit -m 'chore: cleanup repository'"
```

---

## 🎯 Expected Results

### **Before Cleanup:**

```
- 25 markdown files
- 3.7 MB build artifacts
- 7-8 unused code files
- Cluttered root directory
```

### **After Cleanup:**

```
- 5-7 essential docs + docs/archive/
- 0 MB build artifacts (in .gitignore)
- Only actively used code
- Clean, professional structure
```

**Repository size reduction:** ~4-5 MB

---

## 📁 Final Structure

```
WebCartographer/
├── README.md                        # Main docs
├── QUICK-START.md                   # Install guide
├── DEPLOYMENT-GUIDE.md              # Full deployment
├── PERFORMANCE-OPTIMIZATION.md      # Tuning
├── LICENSE                          # Legal
├── flake.nix                        # Nix dev environment
├── WebCartographer.sln              # Solution
├── .gitignore                       # Ignore build artifacts
├── assets/                          # Logo, preview
├── docs/
│   └── archive/                     # Development history
├── WebCartographer/                 # Main server mod
│   ├── WebCartographer.csproj
│   ├── WebCartographer.cs
│   ├── Config.cs
│   ├── Services/
│   │   ├── DataCollectorImproved.cs
│   │   ├── HistoricalTracker.cs
│   │   ├── HistoricalTrackerOptimized.cs
│   │   └── StaticFileServer.cs
│   ├── Models/
│   ├── GeoJson/
│   ├── html/                        # Web UI
│   └── modinfo.json
├── WebCartographerColorExporter/    # Client mod
├── WebCartographer.Tests/           # Tests (future)
├── build/                           # Cake build (optional)
└── WebCartographer-v2.0.0.tar.gz    # Release package
```

---

## ✅ Cleanup Checklist

- [ ] Backup repository: `git branch cleanup-backup`
- [ ] Create `docs/archive/` directory
- [ ] Move development docs to archive
- [ ] Delete outdated docs
- [ ] Remove unused code files
- [ ] Update `.gitignore`
- [ ] Remove build artifacts from git
- [ ] Remove duplicate tarballs
- [ ] Update README.md with merged content
- [ ] Test build: `nix develop --command bash -c "cd WebCartographer && dotnet build"`
- [ ] Commit: `git commit -m "chore: cleanup repository structure"`
- [ ] Verify package still works

---

## 🎓 Benefits

1. **Easier Navigation** - Contributors find docs quickly
2. **Smaller Clone Size** - No unnecessary build artifacts
3. **Professional Appearance** - Clean, organized structure
4. **Better Maintenance** - Clear separation of active vs archived docs
5. **Faster CI/CD** - Less to process

---

## 🚨 IMPORTANT

**Before running cleanup:**

1. ✅ Create backup branch: `git branch cleanup-backup`
2. ✅ Ensure `WebCartographer-v2.0.0.tar.gz` works
3. ✅ Test build after .gitignore changes
4. ✅ Review with `git status` before committing

---

**Ready to clean? Run the cleanup script or execute actions manually!**
