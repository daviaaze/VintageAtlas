# How to Use Port 80 with WebCartographer

## The Problem

Port 80 (HTTP) and 443 (HTTPS) are **privileged ports** on Linux that require root access. Vintage Story runs as a normal user, so the mod cannot bind to port 80 directly.

## ✅ Recommended Solutions

### Solution 1: Use Port Forwarding (iptables)

**Best for**: Production servers where you want `http://server/` to work

Keep the mod on port 42421 (or 8080) and forward port 80 to it:

```bash
# Forward port 80 to 8080 (or whatever port your mod uses)
sudo iptables -t nat -A PREROUTING -p tcp --dport 80 -j REDIRECT --to-port 8080

# Make persistent (Debian/Ubuntu)
sudo apt-get install iptables-persistent
sudo netfilter-persistent save

# Make persistent (RHEL/CentOS/Fedora)
sudo service iptables save
```

**Configuration**:
```json
{
  "LiveServerPort": 8080
}
```

**Access**: `http://your-server/` (automatically forwards to port 8080)

---

### Solution 2: Use Reverse Proxy (nginx/Apache)

**Best for**: Advanced setups with SSL, multiple services, or custom domains

#### nginx Example

1. **Install nginx**:
   ```bash
   sudo apt-get install nginx
   ```

2. **Configure nginx** (`/etc/nginx/sites-available/webcartographer`):
   ```nginx
   server {
       listen 80;
       server_name your-domain.com;  # or your server IP
       
       location / {
           proxy_pass http://localhost:8080;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection 'upgrade';
           proxy_set_header Host $host;
           proxy_cache_bypass $http_upgrade;
           
           # CORS headers (if needed)
           add_header Access-Control-Allow-Origin *;
       }
   }
   ```

3. **Enable and restart**:
   ```bash
   sudo ln -s /etc/nginx/sites-available/webcartographer /etc/nginx/sites-enabled/
   sudo nginx -t
   sudo systemctl restart nginx
   ```

**Mod configuration**:
```json
{
  "LiveServerPort": 8080
}
```

**Access**: `http://your-domain.com/` or `http://your-server-ip/`

#### Apache Example

1. **Install Apache**:
   ```bash
   sudo apt-get install apache2
   sudo a2enmod proxy proxy_http
   ```

2. **Configure Apache** (`/etc/apache2/sites-available/webcartographer.conf`):
   ```apache
   <VirtualHost *:80>
       ServerName your-domain.com
       
       ProxyPreserveHost On
       ProxyPass / http://localhost:8080/
       ProxyPassReverse / http://localhost:8080/
       
       # CORS headers
       Header set Access-Control-Allow-Origin "*"
   </VirtualHost>
   ```

3. **Enable and restart**:
   ```bash
   sudo a2ensite webcartographer
   sudo systemctl restart apache2
   ```

---

### Solution 3: Use authbind (Simple, Linux-specific)

**Best for**: Quick setup without iptables or reverse proxy

1. **Install authbind**:
   ```bash
   sudo apt-get install authbind
   ```

2. **Allow port 80 for your user**:
   ```bash
   sudo touch /etc/authbind/byport/80
   sudo chmod 500 /etc/authbind/byport/80
   sudo chown $USER /etc/authbind/byport/80
   ```

3. **Run Vintage Story with authbind**:
   ```bash
   authbind --deep ./VintagestoryServer
   ```

**Mod configuration**:
```json
{
  "LiveServerPort": 80
}
```

⚠️ **Downside**: Must always run VS with `authbind --deep`

---

### Solution 4: Use setcap (Capability-based, Linux)

**Best for**: Allowing specific binary to bind to low ports

⚠️ **WARNING**: This gives the .NET runtime permission to use any privileged port, which is a security risk!

```bash
# Find dotnet binary
which dotnet

# Grant cap_net_bind_service capability
sudo setcap 'cap_net_bind_service=+ep' /path/to/dotnet

# To remove later:
sudo setcap -r /path/to/dotnet
```

**Mod configuration**:
```json
{
  "LiveServerPort": 80
}
```

⚠️ **Not Recommended**: Security risk, affects all .NET applications

---

### Solution 5: Run as Root (NOT RECOMMENDED)

**Don't do this!** Running Vintage Story as root is a security risk.

```bash
# ❌ DON'T DO THIS
sudo ./VintagestoryServer
```

**Why it's bad**:
- If VS or any mod has a vulnerability, attacker gets root access
- Accidental file operations happen as root
- Against best practices

---

## 🎯 Recommended Approach by Use Case

### Home/LAN Server
**Use high port**: Just use 8080 or 42421
```json
{"LiveServerPort": 8080}
```
Access: `http://192.168.1.x:8080/`

### Public Server (Basic)
**Use iptables forwarding**:
```bash
sudo iptables -t nat -A PREROUTING -p tcp --dport 80 -j REDIRECT --to-port 8080
```
Access: `http://your-domain.com/`

### Public Server (Production)
**Use nginx reverse proxy**:
- Handles SSL/HTTPS
- Better performance
- Can serve multiple services
- Professional setup

### Testing/Development
**Use high port**: Keep it simple
```json
{"LiveServerPort": 8080}
```

---

## 📝 Current Mod Configuration

Edit `ModConfig/WebCartographerConfig.json`:

### For Port 8080 (Recommended)
```json
{
  "EnableLiveServer": true,
  "LiveServerPort": 8080,
  "LiveServerEndpoint": "status",
  "AutoExportMap": true,
  "MapExportIntervalMs": 300000
}
```

### For Default Port (Game Port + 1)
```json
{
  "EnableLiveServer": true,
  "LiveServerPort": null,  // null = auto (game port + 1)
  "LiveServerEndpoint": "status",
  "AutoExportMap": true,
  "MapExportIntervalMs": 300000
}
```

---

## 🔍 Troubleshooting

### Check if port is in use
```bash
# See what's using port 80
sudo netstat -tlnp | grep :80
# or
sudo lsof -i :80
```

### Check if forwarding is active
```bash
# List iptables rules
sudo iptables -t nat -L -n -v | grep 80
```

### Test the mod's actual port
```bash
# Test the mod directly on its high port
curl http://localhost:8080/api/status

# If that works, the mod is fine - just need to forward port 80
```

### Check nginx/Apache status
```bash
# nginx
sudo systemctl status nginx
sudo nginx -t  # Test config

# Apache
sudo systemctl status apache2
sudo apache2ctl configtest  # Test config
```

### Check logs
```bash
# nginx logs
sudo tail -f /var/log/nginx/error.log

# Apache logs
sudo tail -f /var/log/apache2/error.log

# Vintage Story logs
tail -f ~/path/to/VintageStory/Logs/server-main.txt
```

---

## 🌐 Firewall Configuration

Don't forget to open the port in your firewall!

### ufw (Ubuntu/Debian)
```bash
# For direct access to mod
sudo ufw allow 8080/tcp

# If using port 80 forwarding
sudo ufw allow 80/tcp
```

### firewalld (RHEL/CentOS/Fedora)
```bash
# For direct access to mod
sudo firewall-cmd --permanent --add-port=8080/tcp
sudo firewall-cmd --reload

# If using port 80 forwarding
sudo firewall-cmd --permanent --add-port=80/tcp
sudo firewall-cmd --reload
```

### iptables
```bash
# For direct access to mod
sudo iptables -A INPUT -p tcp --dport 8080 -j ACCEPT

# If using port 80 forwarding
sudo iptables -A INPUT -p tcp --dport 80 -j ACCEPT

# Save rules
sudo iptables-save | sudo tee /etc/iptables/rules.v4
```

---

## ✅ Quick Setup Guide

**I just want it to work on port 80!**

1. **Set mod to port 8080**:
   ```json
   {"LiveServerPort": 8080}
   ```

2. **Forward port 80 to 8080**:
   ```bash
   sudo iptables -t nat -A PREROUTING -p tcp --dport 80 -j REDIRECT --to-port 8080
   sudo iptables-save | sudo tee /etc/iptables/rules.v4
   ```

3. **Open firewall**:
   ```bash
   sudo ufw allow 80/tcp
   sudo ufw allow 8080/tcp
   ```

4. **Restart VS server**

5. **Access**: `http://your-server/`

Done! 🎉

---

## 🔒 Security Notes

- **Never run VS as root**
- **Use HTTPS in production** (requires reverse proxy)
- **Restrict access with firewall rules** if needed
- **Keep VS and mods updated**
- **Use strong server passwords**

---

## 📚 Further Reading

- [nginx reverse proxy guide](https://www.nginx.com/resources/wiki/start/topics/examples/reverseproxycachingexample/)
- [Apache mod_proxy guide](https://httpd.apache.org/docs/2.4/mod/mod_proxy.html)
- [Linux capabilities](https://man7.org/linux/man-pages/man7/capabilities.7.html)
- [iptables NAT guide](https://www.netfilter.org/documentation/HOWTO/NAT-HOWTO.html)

