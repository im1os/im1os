# RustDesk OSS Remote Support Infrastructure

This runbook documents the standalone RustDesk OSS and Portainer deployment on `im1-dev`.

## Server Architecture

- Host: `im1-dev`
- OS: Ubuntu 24.04.4 LTS
- Public IPv4: `209.126.5.249`
- RustDesk/customer support DNS name: `support.im1os.com`
- Docker management DNS name: `docker.im1os.com`
- RustDesk ID/rendezvous service: `hbbs`
- RustDesk relay service: `hbbr`
- Docker management UI: Portainer Community Edition
- HTTPS reverse proxy: host Nginx with Let's Encrypt certificate

RustDesk runs with Docker host networking, as documented by RustDesk for Linux Compose deployments. Portainer runs on Docker bridge networking and is bound only to localhost; Nginx proxies public HTTPS traffic from `docker.im1os.com:9444` to `127.0.0.1:9000`.

Official references:

- Docker Engine on Ubuntu: https://docs.docker.com/engine/install/ubuntu/
- RustDesk OSS Docker deployment: https://rustdesk.com/docs/en/self-host/rustdesk-server-oss/docker/
- RustDesk client configuration: https://rustdesk.com/docs/en/self-host/client-configuration/
- Portainer CE Docker deployment: https://docs.portainer.io/start/install-ce/server/docker/linux

## Docker Layout

RustDesk:

- Directory: `/opt/rustdesk`
- Compose file: `/opt/rustdesk/compose.yaml`
- Environment file: `/opt/rustdesk/.env`
- Persistent data and identity: `/opt/rustdesk/data`
- Containers: `hbbs`, `hbbr`
- Image: `rustdesk/rustdesk-server:latest`
- Restart policy: `unless-stopped`

Portainer:

- Directory: `/opt/portainer`
- Compose file: `/opt/portainer/compose.yaml`
- Environment file: `/opt/portainer/.env`
- Persistent Docker volume: `portainer_data`
- Volume mountpoint: `/var/lib/docker/volumes/portainer_data/_data`
- Container: `portainer`
- Image: `portainer/portainer-ce:lts`
- Restart policy: `unless-stopped`

## Installed Containers

```bash
sudo docker ps
```

Expected services:

- `hbbs`: RustDesk ID/rendezvous server
- `hbbr`: RustDesk relay server
- `portainer`: Portainer CE web UI

## DNS Configuration

Cloudflare DNS has these DNS-only A records:

- Name: `support.im1os.com`
- Type: `A`
- Value: `209.126.5.249`
- Proxy status: DNS Only, gray cloud

- Name: `docker.im1os.com`
- Type: `A`
- Value: `209.126.5.249`
- Proxy status: DNS Only, gray cloud

Do not proxy RustDesk through Cloudflare. RustDesk requires direct TCP/UDP connectivity to the host ports.

`support.im1os.com` is reserved for the customer-facing support portal and is also the RustDesk client hostname for ports `21115` through `21119`. `docker.im1os.com:9444` is the Portainer management endpoint. Portainer is not served on standard HTTPS port `443`.

## HTTPS and Nginx

Support portal Nginx site:

- `/etc/nginx/sites-available/support-im1os-portal`
- `/etc/nginx/sites-enabled/support-im1os-portal`

Support portal static root:

- `/opt/support-portal`

Support portal certificate:

- `/etc/letsencrypt/live/support.im1os.com/fullchain.pem`
- `/etc/letsencrypt/live/support.im1os.com/privkey.pem`

The customer-facing support portal is available at:

```text
https://support.im1os.com
```

The Windows download button is configured in `/opt/support-portal/config.js`:

```javascript
window.IM1_REMOTE_SUPPORT_WINDOWS_DOWNLOAD_URL = "/download/windows";
```

`/download/windows` serves a local iM1 setup command file. This is the current production download because it avoids the unsigned EXE SmartScreen warning seen with the wrapper executable:

- `/opt/support-portal/downloads/im1-remote-support-windows.cmd`

That command downloads and runs this PowerShell helper:

- `/opt/support-portal/downloads/im1-remote-support-windows.ps1`

The helper downloads the official Windows client from upstream, writes the iM1 server settings into the user's RustDesk client config, imports that config, and launches the client.

An unsigned EXE wrapper is also available for testing only:

- `/download/windows-exe`
- `/opt/support-portal/downloads/iM1-Remote-Support-Windows.exe`

Do not make the EXE the primary customer download until it is code-signed.

Wrapper source is stored in the repository at:

- `support-portal/downloads/windows-setup/`

Build command:

```powershell
dotnet publish support-portal\downloads\windows-setup\Im1RemoteSupportSetup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o support-portal\downloads\publish
```

Current wrapper values:

```csharp
const string clientDownloadUrl = "https://github.com/rustdesk/rustdesk/releases/download/1.4.9/rustdesk-1.4.9-x86_64.exe";
const string supportHost = "support.im1os.com";
const string serverKey = "0t5yzcyqDFe2yj4X50zdRemrGtotzXD2Pxj8P4b43zI=";
```

To replace the temporary upstream client with a future branded iM1 client, update the download URL in `/opt/support-portal/downloads/im1-remote-support-windows.ps1`. If using the EXE wrapper after code-signing, update `clientDownloadUrl`, rebuild the wrapper, redeploy `/opt/support-portal/downloads/iM1-Remote-Support-Windows.exe`, and reload Nginx if the path changes.

Portainer Nginx site:

- `/etc/nginx/sites-available/docker-im1os-portainer`
- `/etc/nginx/sites-enabled/docker-im1os-portainer`

Portainer certificate:

- `/etc/letsencrypt/live/docker.im1os.com/fullchain.pem`
- `/etc/letsencrypt/live/docker.im1os.com/privkey.pem`

Portainer is available at:

```text
https://docker.im1os.com:9444
```

`support.im1os.com` has a separate Nginx vhost at `/etc/nginx/sites-available/support-im1os-portal` for the customer-facing support portal. It does not proxy to Portainer.

Portainer native ports are not publicly exposed. They are bound to localhost:

- `127.0.0.1:9000`
- `127.0.0.1:9443`

## Firewall Configuration

UFW is active. Existing SSH, HTTP, and HTTPS rules remain in place. RustDesk-specific rules:

- `21115/tcp`
- `21116/tcp`
- `21117/tcp`
- `21118/tcp`
- `21119/tcp`
- `21116/udp`

Check rules:

```bash
sudo ufw status verbose
```

No UFW rule is required for Portainer because public access is through Nginx on HTTPS.

## Port Usage

| Port | Protocol | Service | Purpose |
| --- | --- | --- | --- |
| 21115 | TCP | `hbbs` | NAT type test |
| 21116 | TCP | `hbbs` | TCP hole punching and connection service |
| 21116 | UDP | `hbbs` | ID registration and heartbeat |
| 21117 | TCP | `hbbr` | Relay service |
| 21118 | TCP | `hbbs` | Web client support |
| 21119 | TCP | `hbbr` | Web client relay support |
| 443 | TCP | Nginx | Standard HTTPS for iM1 marketing/dev and `support.im1os.com` portal |
| 9444 | TCP | Nginx | Alternate HTTPS port for Portainer at `docker.im1os.com:9444` |
| 80 | TCP | Nginx | HTTP redirect and Let's Encrypt challenge |
| 9000 | TCP localhost only | Portainer | Internal HTTP UI behind Nginx |
| 9443 | TCP localhost only | Portainer | Local HTTPS access via SSH tunnel |

## RustDesk Server Keys

RustDesk server identity is stored in:

- Private key: `/opt/rustdesk/data/id_ed25519`
- Public key: `/opt/rustdesk/data/id_ed25519.pub`

The keypair was generated once before normal Compose startup. Both `hbbs` and `hbbr` mount the same `/opt/rustdesk/data` directory. Preserve these files during backup, restore, upgrade, and migration. Deleting or regenerating them changes the server identity and can break client trust.

Public key value for RustDesk client `Key` field:

```text
0t5yzcyqDFe2yj4X50zdRemrGtotzXD2Pxj8P4b43zI=
```

## Client Configuration

RustDesk desktop clients should use:

```text
ID Server: support.im1os.com
Relay Server: support.im1os.com
Key: 0t5yzcyqDFe2yj4X50zdRemrGtotzXD2Pxj8P4b43zI=
```

## Docker Commands

RustDesk:

```bash
cd /opt/rustdesk
sudo docker compose ps
sudo docker compose up -d
sudo docker compose stop
sudo docker compose start
sudo docker compose restart
sudo docker compose logs -f
sudo docker compose pull
sudo docker compose up -d
```

Portainer:

```bash
cd /opt/portainer
sudo docker compose ps
sudo docker compose up -d
sudo docker compose stop
sudo docker compose start
sudo docker compose restart
sudo docker compose logs -f
sudo docker compose pull
sudo docker compose up -d
```

All containers:

```bash
sudo docker ps
sudo docker logs hbbs
sudo docker logs hbbr
sudo docker logs portainer
```

## Backup Procedure

Create a backup directory:

```bash
sudo install -d -m 0700 /root/im1-remote-support-backups
```

Back up RustDesk, including identity keys:

```bash
sudo tar -C /opt -czf /root/im1-remote-support-backups/rustdesk-$(date +%F).tgz rustdesk
```

Back up Portainer:

```bash
sudo docker run --rm \
  -v portainer_data:/data \
  -v /root/im1-remote-support-backups:/backup \
  ubuntu:24.04 \
  tar -C /data -czf /backup/portainer-data-$(date +%F).tgz .
```

Back up Nginx and certificate metadata if moving the HTTPS endpoint:

```bash
sudo tar -czf /root/im1-remote-support-backups/support-nginx-letsencrypt-$(date +%F).tgz \
  /etc/nginx/sites-available/docker-im1os-portainer \
  /etc/nginx/sites-enabled/docker-im1os-portainer \
  /etc/nginx/sites-available/support-im1os-portal \
  /etc/nginx/sites-enabled/support-im1os-portal \
  /etc/letsencrypt/renewal/docker.im1os.com.conf \
  /etc/letsencrypt/live/docker.im1os.com \
  /etc/letsencrypt/archive/docker.im1os.com \
  /etc/letsencrypt/renewal/support.im1os.com.conf \
  /etc/letsencrypt/live/support.im1os.com \
  /etc/letsencrypt/archive/support.im1os.com
```

## Restore Procedure

1. Install Docker Engine and the Docker Compose plugin from Docker's official APT repository.
2. Stop any existing RustDesk containers:

```bash
cd /opt/rustdesk
sudo docker compose down
```

3. Restore RustDesk:

```bash
sudo tar -C /opt -xzf /root/im1-remote-support-backups/rustdesk-YYYY-MM-DD.tgz
sudo chmod 0600 /opt/rustdesk/data/id_ed25519
sudo chmod 0644 /opt/rustdesk/data/id_ed25519.pub
cd /opt/rustdesk
sudo docker compose up -d
```

4. Restore Portainer:

```bash
sudo docker volume create portainer_data
sudo docker run --rm \
  -v portainer_data:/data \
  -v /root/im1-remote-support-backups:/backup \
  ubuntu:24.04 \
  tar -C /data -xzf /backup/portainer-data-YYYY-MM-DD.tgz
cd /opt/portainer
sudo docker compose up -d
```

5. Reapply UFW rules and DNS as documented above.

## Upgrade Procedure

RustDesk:

```bash
cd /opt/rustdesk
sudo docker compose pull
sudo docker compose up -d
sudo docker compose logs --tail=100 hbbs hbbr
```

Portainer:

```bash
cd /opt/portainer
sudo docker compose pull
sudo docker compose up -d
sudo docker compose logs --tail=100 portainer
```

Docker Engine:

```bash
sudo apt-get update
sudo apt-get install docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo systemctl status docker
```

## Validation

Docker:

```bash
sudo docker version
sudo docker compose version
sudo systemctl status docker
```

Containers:

```bash
sudo docker ps
sudo docker inspect -f '{{.Name}} restart={{.HostConfig.RestartPolicy.Name}} state={{.State.Status}}' hbbs hbbr portainer
```

Listeners:

```bash
sudo ss -tulpn | grep -E '21115|21116|21117|21118|21119'
```

Portainer:

```bash
curl -fsS http://127.0.0.1:9000/api/status
curl -kfsS https://127.0.0.1:9443/api/status
curl -I https://docker.im1os.com:9444
```

DNS:

```bash
dig +short support.im1os.com A
dig +short docker.im1os.com A
dig @1.1.1.1 +short support.im1os.com A
dig @1.1.1.1 +short docker.im1os.com A
```

Firewall:

```bash
sudo ufw status verbose
```

Client validation:

1. Open a RustDesk desktop client.
2. Set `ID Server` to `support.im1os.com`.
3. Set `Relay Server` to `support.im1os.com`.
4. Connect to a second RustDesk client using the same server settings.
5. Confirm the session establishes and traffic relays when direct peer-to-peer connectivity is unavailable.

## Migration to a Dedicated Remote Support Server

To preserve client trust, migrate the RustDesk keypair and DNS name together.

1. Build the new Ubuntu host and install Docker Engine from Docker's official APT repository.
2. Copy `/opt/rustdesk` from `im1-dev` to the new host, preserving ownership and permissions.
3. Confirm these files exist on the new host before starting containers:

```bash
sudo ls -l /opt/rustdesk/data/id_ed25519 /opt/rustdesk/data/id_ed25519.pub
```

4. Start RustDesk on the new host:

```bash
cd /opt/rustdesk
sudo docker compose up -d
```

5. Recreate the UFW rules for ports `21115/tcp`, `21116/tcp`, `21117/tcp`, `21118/tcp`, `21119/tcp`, and `21116/udp`.
6. Update the Cloudflare DNS-only A records for `support.im1os.com` and `docker.im1os.com` to the new public IP.
7. Verify public DNS and external TCP connectivity.
8. Test a RustDesk desktop session using the same client settings.
9. Keep the old server stopped but intact until clients have successfully connected through the new host.

Do not start a new RustDesk deployment with an empty data directory on the new host. That would generate a new keypair and change server identity.
