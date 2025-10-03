# Frontend Strict Configuration Changes

**Date:** October 3, 2025

## Overview

Removed all fallback values from the frontend to ensure it only works with proper server configuration. This makes configuration issues immediately visible instead of silently using fallback values.

## Changes Made

### 1. `src/utils/mapConfig.ts`

**Before:**
- Had fallback values for every configuration option
- Silently used defaults if config was missing or invalid
- Warnings logged but app continued running

**After:**
- ❌ **NO FALLBACKS** - All config must come from server
- Throws clear errors if config is missing or invalid
- Better validation with descriptive error messages
- Added `isConfigLoaded()` helper function

**Key changes:**
```typescript
// OLD: function getConfig<K>(key: K, fallback: T): T
// NEW: function getConfig<K>(key: K): T (throws if missing)

// Removed fallbacks from:
- worldExtent()
- worldOrigin()
- tileResolutions()
- worldResolutions()
- defaultCenter()
- defaultZoom()
- minZoom()
- maxZoom()
- isAbsolutePositions()
- getSpawnPosition()
- getTileOffset()
```

### 2. `src/services/api/mapConfig.ts`

**Before:**
- Silent error handling in `fetchMapConfig()`
- Fallback values in `fetchWorldExtent()`
- No validation of server response

**After:**
- ❌ **NO FALLBACKS** - Throws on any error
- Validates all required fields from server
- Better error messages with emoji indicators
- Clear logging of fetch progress

**Key changes:**
```typescript
// Added validation for required fields:
const requiredFields = [
  'worldExtent', 'worldOrigin', 'defaultCenter', 'defaultZoom',
  'minZoom', 'maxZoom', 'tileSize', 'tileResolutions', 'viewResolutions',
  'spawnPosition', 'absolutePositions'
];

// Throws if any field is missing
if (missingFields.length > 0) {
  throw new Error(`Server config is missing: ${missingFields.join(', ')}`);
}
```

### 3. `src/main.ts`

**Before:**
- Logged warning on config failure
- Continued app initialization anyway
- No user feedback on failure

**After:**
- ❌ **FATAL ERROR** - Stops app initialization
- Shows user-friendly error page
- Provides troubleshooting steps
- Displays full error details in expandable section

**Error page includes:**
- Clear error message
- Detailed error information
- Troubleshooting steps
- Styled for dark theme

## Benefits

### 1. **Immediate Problem Detection**
- Configuration issues are now immediately visible
- No more silent failures with fallback values
- Easier to debug server-side configuration problems

### 2. **Better Error Messages**
- Clear indication of what's wrong
- Shows which config fields are missing
- Provides actionable troubleshooting steps

### 3. **Proper Error Handling**
- All config access throws on error
- No more inconsistent behavior from partial configs
- Frontend either works perfectly or fails visibly

### 4. **Developer Experience**
- Console logs with emoji indicators (✅ ❌ 📡 ⏳)
- Structured error messages
- Easy to spot configuration problems

## Testing

To test the strict configuration:

### 1. **Normal Operation**
```bash
# Start server with VintageAtlas mod
# Frontend should load with:
# ✅ [MapConfig] Configuration loaded from server
# ✅ [VintageAtlas] Application initialized
```

### 2. **Missing Server**
```bash
# Start frontend without backend
# Should show error page:
# ❌ Cannot Load Map Configuration
```

### 3. **Invalid Config**
```bash
# If server sends incomplete config
# Frontend will show which fields are missing:
# ❌ Server config is missing required fields: worldExtent, tileSize, ...
```

## Error Scenarios

### 1. Server Not Running
```
❌ Cannot fetch map configuration from server: 
   HTTP 404: Not Found
```

### 2. Missing Config Fields
```
❌ Server config is missing required fields: 
   worldExtent, defaultZoom, tileResolutions
```

### 3. Invalid Config Values
```
❌ Invalid extent from server: [undefined, undefined, undefined, undefined]
   Must be [minX, minZ, maxX, maxZ] with finite numbers.
```

### 4. Config Not Initialized
```
❌ Map config not initialized! Attempted to access: worldExtent
```

## Console Output Examples

### Success Case
```
🚀 [VintageAtlas] Initializing map configuration from server...
📡 [MapConfigAPI] Fetching from /api/map-config...
✅ [MapConfigAPI] Config validated and cached
✅ [MapConfig] Configuration loaded from server: {...}
   Tile offset: [0, 0]
   World extent: [-512000, -512000, 512000, 512000]
   Absolute positions: false
   World extent validated: [-512000, -512000, 512000, 512000]
   World origin validated: [-512000, 512000]
✅ [VintageAtlas] Application initialized with server configuration
```

### Failure Case
```
🚀 [VintageAtlas] Initializing map configuration from server...
📡 [MapConfigAPI] Fetching from /api/map-config...
❌ [MapConfigAPI] Failed to fetch config: TypeError: Failed to fetch
❌ [VintageAtlas] FATAL: Cannot start without server configuration!
Error: Cannot fetch map configuration from server: Failed to fetch
```

## Migration Guide

If you're upgrading from the old version with fallbacks:

### What Changed
1. Frontend **requires** all config from server
2. No more default/fallback values
3. App won't start if config is missing

### What You Need
Server must provide ALL of these fields in `/api/map-config`:
- `worldExtent`: `[minX, minZ, maxX, maxZ]`
- `worldOrigin`: `[x, z]`
- `defaultCenter`: `[x, z]`
- `defaultZoom`: `number`
- `minZoom`: `number`
- `maxZoom`: `number`
- `tileSize`: `number`
- `tileResolutions`: `number[]`
- `viewResolutions`: `number[]`
- `spawnPosition`: `[x, z]`
- `absolutePositions`: `boolean`
- `tileOffset`: `[x, z]` (can be `[0, 0]` if not used)

### Debugging Tips
1. Check browser console for detailed error messages
2. Verify `/api/map-config` endpoint is accessible
3. Validate JSON response has all required fields
4. Check server logs for backend errors

## Files Modified

1. ✅ `frontend/src/utils/mapConfig.ts` - Removed all fallbacks
2. ✅ `frontend/src/services/api/mapConfig.ts` - Added validation, removed fallbacks
3. ✅ `frontend/src/main.ts` - Added error page on config failure

## Backward Compatibility

⚠️ **BREAKING CHANGE**

This is a breaking change - the frontend will NOT work with:
- Old server versions that don't provide full config
- Servers with incomplete configuration
- Manual testing without backend

The old "fallback mode" has been completely removed to ensure configuration correctness.

## Next Steps

1. ✅ Test with live server
2. ✅ Verify error messages are helpful
3. ✅ Ensure all required config fields are provided
4. Document required server-side configuration
5. Update deployment documentation

## Questions?

If you encounter issues:
1. Check the browser console for detailed errors
2. Verify the server is running and accessible
3. Check `/api/map-config` returns valid JSON
4. Review server logs for backend errors

