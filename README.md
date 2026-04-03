# GoonNet

## Making your stream reachable beyond LAN (public internet)

By default, GoonNet serves audio over HTTP at:

- `http://localhost:8000/stream` (local machine), or
- `http://<your-lan-ip>:8000/stream` (devices on your LAN)

If you want **anyone with a URL** to listen from outside your home/network, do this:

### 1) Start streaming in GoonNet

1. Open **Web Streaming**.
2. Pick your port (default `8000`).
3. Click **START STREAMING**.
4. Confirm the URL works on another device on the same LAN first.

### 2) Allow the port in Windows Firewall (host machine)

Run PowerShell as Administrator:

```powershell
New-NetFirewallRule -DisplayName "GoonNet Stream 8000" -Direction Inbound -Protocol TCP -LocalPort 8000 -Action Allow
```

If you changed the stream port, replace `8000`.

### 3) Set router port forwarding (NAT)

In your router admin page, add a forward:

- **External port:** `8000` (or your chosen port)
- **Internal IP:** your GoonNet PC LAN IP (example `192.168.1.50`)
- **Internal port:** `8000`
- **Protocol:** TCP

### 4) Find your public IP and test from outside

Your public URL will usually be:

- `http://<your-public-ip>:8000/stream`

Important: test using mobile data or a different network (not your Wi‑Fi), because many routers do not support NAT loopback.

### 5) (Recommended) Use a DNS name instead of raw IP

Use Dynamic DNS (Cloudflare, DuckDNS, No-IP, etc.) so listeners get:

- `http://radio.yourdomain.com:8000/stream`

### 6) Understand ISP limitations

If it still fails, your ISP may block inbound ports or use CGNAT. In that case:

- ask ISP for a public IPv4/static IP, or
- use a tunnel/reverse proxy solution (for example Cloudflare Tunnel) and expose `/stream` through that endpoint.

---

## Security notes (important)

- This stream endpoint is plain HTTP (not encrypted) and intended for simple/public radio-style streaming.
- Do **not** expose management/control endpoints (like Telnet) to the internet.
- Use a strong OS account password and keep your machine updated.
