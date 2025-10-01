# Nginx Compatibility Guide

WebCartographer now fully supports deployment behind nginx reverse proxy, including both root and sub-path configurations!

## 🎯 What's Been Implemented

### ✅ Full Nginx Best Practices Support

1. **Correct MIME Types** - All static files served with proper Content-Type headers
2. **CORS Headers** - Cross-origin requests fully supported
3. **Health Check Endpoint** - `/api/health` for monitoring
4. **Proxy Header Support** - Real client IP detection via X-Forwarded-For
5. **Sub-path Deployment** - Works at root `/` or sub-paths `/vintagestory/`
6. **Caching Headers** - Static assets cached for optimal performance

---

## 📋 Configuration

### Option 1: Root Path (Default)

**WebCartographerConfig.json:**
```json
{
  "EnableLiveServer": true,
  "LiveServerPort": 8080,
  "BasePath": "/"
}
```

**Nginx config:**
```nginx
server {
    listen 80;
    server_name vintagestory.example.com;

    location / {
        proxy_pass http://localhost:8080/;
        proxy_http_version 1.1;
        
        # Proxy headers for real client IP
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header Host $host;
        
        # WebSocket support (if needed in future)
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

**Access:**
- Web UI: `http://vintagestory.example.com/`
- API: `http://vintagestory.example.com/api/status`
- Health: `http://vintagestory.example.com/api/health`

---

### Option 2: Sub-path Deployment

Perfect for multi-service setups where you want Vintagestory at a sub-path alongside other apps.

**WebCartographerConfig.json:**
```json
{
  "EnableLiveServer": true,
  "LiveServerPort": 8080,
  "BasePath": "/vintagestory/"
}
```

**Nginx config:**
```nginx
server {
    listen 80;
    server_name example.com;

    # Your main site
    location / {
        # ... your main app ...
    }
    
    # Vintagestory map at /vintagestory/
    location /vintagestory/ {
        proxy_pass http://localhost:8080/;
        proxy_http_version 1.1;
        
        # Proxy headers
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header Host $host;
        
        # WebSocket support
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

**Access:**
- Web UI: `http://example.com/vintagestory/`
- API: `http://example.com/vintagestory/api/status`
- Health: `http://example.com/vintagestory/api/health`

---

## 🔧 How It Works

### 1. **Base Path Injection**

The C# server dynamically injects the base path into HTML at runtime:

```html
<!-- Before (template) -->
<base href="__BASE_PATH__">

<!-- After (served to client) -->
<base href="/vintagestory/">
```

All relative URLs in HTML now automatically use the correct base!

### 2. **JavaScript Base Path Detection**

`liveLayers.js` automatically detects the base path:

```javascript
function getBasePath() {
  const baseEl = document.querySelector('base[href]');
  if (baseEl) {
    const href = baseEl.getAttribute('href');
    if (href && href !== '__BASE_PATH__') {
      return href.endsWith('/') ? href : href + '/';
    }
  }
  return '/';
}

const BASE_PATH = getBasePath();
const INTEGRATED_API_URL = BASE_PATH + 'api/status';
```

### 3. **Proxy Header Support**

Server reads `X-Forwarded-For` to get real client IP:

```csharp
private string GetClientIp(HttpListenerContext context)
{
    var forwardedFor = context.Request.Headers["X-Forwarded-For"];
    if (!string.IsNullOrEmpty(forwardedFor))
    {
        var ips = forwardedFor.Split(',');
        return ips[0].Trim(); // First IP is real client
    }
    return context.Request.RemoteEndPoint?.Address?.ToString() ?? "unknown";
}
```

---

## 🚀 Quick Start

### 1. Update Your Config

Edit `ModConfig/WebCartographerConfig.json`:

```json
{
  "Mode": 4,
  "OutputDirectory": "/path/to/webmap",
  "EnableLiveServer": true,
  "LiveServerPort": 8080,
  "BasePath": "/",
  "EnableCORS": true
}
```

### 2. Configure NixOS (or your system)

**NixOS example:**

```nix
services.nginx = {
  enable = true;
  
  virtualHosts."vintagestory.example.com" = {
    locations."/" = {
      proxyPass = "http://localhost:8080/";
      proxyWebsockets = true;
      extraConfig = ''
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header Host $host;
      '';
    };
  };
};
```

### 3. Test Direct Connection First

Before setting up nginx, verify the server works:

```bash
# On your server
curl http://localhost:8080/
curl http://localhost:8080/api/status
curl http://localhost:8080/api/health
```

Expected responses:
- `/` → HTML page
- `/api/status` → JSON with server data
- `/api/health` → JSON: `{"status":"ok","timestamp":...}`

### 4. Test Through Nginx

```bash
curl http://vintagestory.example.com/
curl http://vintagestory.example.com/api/status
curl http://vintagestory.example.com/api/health
```

---

## ✅ Verification Checklist

- [ ] Server runs on port 8080 (or configured port)
- [ ] Files exist in output directory: `html/index.html`, `html/lib/ol.css`, etc.
- [ ] `/api/status` returns valid JSON
- [ ] `/api/health` returns `{"status":"ok"}`
- [ ] Nginx proxy configured with proper headers
- [ ] CSS/JS files load without MIME type errors
- [ ] Map tiles load correctly
- [ ] Player positions update in real-time

---

## 🐛 Troubleshooting

### MIME Type Errors

**Problem:** Console error: `MIME type "text/plain" is not "text/css"`

**Solution:** Already fixed! StaticFileServer sets correct MIME types for all files.

### 404 Not Found

**Problem:** Files not found after nginx setup

**Solutions:**
1. Check `BasePath` in config matches nginx `location` directive
2. Verify nginx `proxy_pass` ends with `/`
3. Ensure output directory contains `html/` folder with all files

### Client IP Shows as `::1` or `127.0.0.1`

**Problem:** Server sees localhost instead of real client IP

**Solution:** Add proxy headers to nginx config:
```nginx
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header X-Real-IP $remote_addr;
```

### Base Path Not Working

**Problem:** App breaks when served at `/vintagestory/`

**Check:**
1. `BasePath` in `WebCartographerConfig.json` is `"/vintagestory/"`
2. Browser inspector shows `<base href="/vintagestory/">` in HTML
3. JavaScript console doesn't show base path detection errors

---

## 📊 API Endpoints

All endpoints respect the configured `BasePath`:

| Endpoint | Method | Description | Response |
|----------|--------|-------------|----------|
| `/` | GET | Web UI (index.html) | HTML |
| `/api/status` | GET | Server status, players, animals | JSON |
| `/api/health` | GET | Health check | JSON |
| `/data/world/{z}/{x}_{y}.png` | GET | Map tiles | Image |
| `/lib/ol.css` | GET | Static assets | CSS/JS/etc |

---

## 🔒 Security Notes

1. **Directory Traversal Protection** - Built-in path sanitization
2. **CORS** - Configurable via `EnableCORS` setting
3. **Client IP Logging** - Real IPs logged via proxy headers
4. **Port Binding** - Binds to all interfaces for docker/VM compatibility

---

## 🌐 Multi-Service Example

Serve multiple apps on one domain:

```nginx
server {
    listen 80;
    server_name example.com;
    
    # Main website
    location / {
        proxy_pass http://localhost:3000/;
    }
    
    # Vintagestory map
    location /vintagestory/ {
        proxy_pass http://localhost:8080/;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
    
    # Other game server
    location /minecraft/ {
        proxy_pass http://localhost:8081/;
    }
}
```

**WebCartographerConfig.json:**
```json
{
  "BasePath": "/vintagestory/",
  "LiveServerPort": 8080
}
```

Now you can access:
- Main site: `http://example.com/`
- Vintagestory: `http://example.com/vintagestory/`
- Minecraft: `http://example.com/minecraft/`

---

## 📝 Summary

Your WebCartographer mod now includes:

✅ **All nginx best practices from the guide**
✅ **Sub-path deployment support**
✅ **Proxy header handling**
✅ **Health checks**
✅ **Proper MIME types**
✅ **CORS support**
✅ **Caching headers**

You can deploy it at root or any sub-path, and nginx will work perfectly!

---

## 🆘 Need Help?

If you encounter issues:

1. Check server logs: `[WebCartographer]` messages
2. Test direct connection (bypass nginx): `curl http://localhost:8080/api/health`
3. Verify nginx config: `sudo nginx -t`
4. Check browser console for JavaScript errors
5. Enable verbose debug logging in Vintagestory server config

**Example debug output:**
```
[WebCartographer] Request from 192.168.1.100: /api/status
[WebCartographer] Using web root: /path/to/webmap/html
```

