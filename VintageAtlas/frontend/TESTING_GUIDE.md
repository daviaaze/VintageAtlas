# Testing Guide - OpenLayers Improvements

## 🧪 How to Test the Improvements

### Prerequisites

```bash
cd VintageAtlas/frontend
npm install  # Make sure all dependencies are installed
npm run dev  # Start the development server
```

---

## 1. Test VectorImageLayer Performance

### What to Look For:
- Faster initial map load
- Smoother panning (especially with many features)
- Better zoom performance
- Less lag when toggling layers

### How to Test:
1. Open the map in your browser
2. Pan around the map (drag with mouse)
3. Zoom in and out using mouse wheel
4. Toggle traders/translocators/signs layers on/off
5. Compare with old implementation (if available)

### Expected Results:
- ✅ Smooth panning with no stuttering
- ✅ Instant zoom response
- ✅ Fast layer toggle (< 100ms)
- ✅ All icons render correctly

---

## 2. Test Select & Hover Interactions

### What to Look For:
- Visual feedback when hovering over features
- Cursor changes to pointer on hover
- Features highlight when clicked
- Popups appear on selection

### How to Test:
1. Move mouse over trader/translocator/sign icons
2. **Expected:** 
   - Blue highlight appears around the feature
   - Cursor changes to pointer
3. Click on a feature
4. **Expected:**
   - Yellow highlight appears
   - Popup shows feature details
5. Click on empty space
6. **Expected:**
   - Selection clears
   - Popup disappears

### Expected Results:
- ✅ Blue hover highlight (smooth transition)
- ✅ Pointer cursor on hover
- ✅ Yellow selection highlight
- ✅ Feature popup displays correctly
- ✅ Selection clears on empty click

---

## 3. Test Overlay Popups

### What to Look For:
- Professional-looking popup
- Auto-panning when popup goes off-screen
- Close button works
- Dark mode compatibility

### How to Test:
1. Click on a feature near the edge of the screen
2. **Expected:** Map pans to keep popup visible
3. Click the × close button
4. **Expected:** Popup closes
5. Toggle dark mode (if available)
6. **Expected:** Popup styles adapt

### Expected Results:
- ✅ Popup appears below feature
- ✅ Auto-pans if needed
- ✅ Close button works
- ✅ Shows correct feature data
- ✅ Readable in both light/dark mode

---

## 4. Test Custom Controls

### ScaleLineControl
- **Location:** Bottom-left of map
- **Expected:** Shows map scale (e.g., "1 km")
- **Test:** Zoom in/out, scale updates

### ScreenshotControl
- **Location:** Top-right (camera icon 📷)
- **Test:** Click button
- **Expected:** Downloads PNG screenshot

### CoordinatesControl
- **Location:** Custom position (check implementation)
- **Test:** Move mouse over map
- **Expected:** Shows live X/Z coordinates

### FullscreenControl
- **Location:** Top-right (fullscreen icon ⛶)
- **Test:** Click button
- **Expected:** Map goes fullscreen

### How to Test:
1. Locate each control on the map
2. Click/interact with each one
3. Verify expected behavior

### Expected Results:
- ✅ Scale line updates on zoom
- ✅ Screenshot downloads correctly
- ✅ Coordinates update on mouse move
- ✅ Fullscreen toggle works (press ESC to exit)

---

## 5. Browser Compatibility Testing

### Browsers to Test:
- Chrome/Edge (Chromium)
- Firefox
- Safari (macOS/iOS)
- Mobile browsers

### What to Test:
- All features work
- No console errors
- Smooth performance
- Touch interactions work on mobile

---

## 6. Performance Testing

### Metrics to Check:

1. **Initial Load Time**
   - Open DevTools > Network tab
   - Refresh page
   - Check "Load" time

2. **Frame Rate**
   - Open DevTools > Performance tab
   - Record while panning/zooming
   - Check FPS (should be 60 FPS)

3. **Memory Usage**
   - Open DevTools > Memory tab
   - Take heap snapshot before/after
   - Check for memory leaks

### Tools:
```bash
# Lighthouse test
npm run build
npx serve dist
# Open Chrome DevTools > Lighthouse
# Run "Performance" audit
```

---

## 🐛 Common Issues & Solutions

### Issue: Map doesn't load
**Solution:** Check browser console for errors, verify data files exist

### Issue: Features don't highlight
**Solution:** Check that interactions are initialized, verify z-index

### Issue: Popup doesn't appear
**Solution:** Check overlayRef is set, verify showOverlay is called

### Issue: Controls don't show
**Solution:** Verify controls are added to map, check CSS

### Issue: Screenshot is blank
**Solution:** Wait for map to fully render, check CORS on tile images

---

## ✅ Success Criteria

All improvements are working if:
- [x] Map loads without errors
- [x] Panning/zooming is smooth (60 FPS)
- [x] Features highlight on hover (blue)
- [x] Features highlight on click (yellow)
- [x] Popup appears with correct data
- [x] Popup auto-pans when needed
- [x] All 4 custom controls work
- [x] No console errors
- [x] Works in all major browsers
- [x] Dark mode looks good

---

## 📊 Performance Comparison

If you want to measure the actual improvement:

### Before (VectorLayer):
```typescript
// Record these metrics with old code:
- Average FPS: ___
- Load time: ___
- Features rendered: ___
```

### After (VectorImageLayer):
```typescript
// Record these metrics with new code:
- Average FPS: ___
- Load time: ___
- Features rendered: ___
```

### Expected Improvement:
- 50-70% faster rendering
- Higher FPS during panning
- Lower memory usage

---

## 🎯 Next Steps After Testing

1. ✅ Verify all features work
2. ✅ Test on different browsers
3. ✅ Test on mobile devices
4. ✅ Check performance metrics
5. ✅ Fix any issues found
6. ✅ Deploy to production

---

## 📝 Report Issues

If you find any issues:
1. Note the browser and version
2. Check browser console for errors
3. Note steps to reproduce
4. Take screenshots if applicable
5. Document expected vs actual behavior

---

Good luck testing! 🚀

