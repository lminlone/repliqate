---
title: Getting Started
weight: 1
cascade:
  type: docs
---

This specific page is about running the repliqate container itself.

## Prerequisites

- Docker Engine 24.0 or later
- Access to container runtime socket
- Storage location for backup data

## Exposing Volumes
- `/var/run/docker.sock`: Required so Repliqate can read labels and control containers.
- `/app/repliqate`: Storage for metadata and backup files.
- `/var/lib/docker/volume`: Repliqate needs direct access to the volume data to be able to back it up. It's possible to give individual access to each volume directly to repliqate without exposing all your volumes: eg `/var/lib/docker/volume/my_volume_name:/var/lib/docker/volume/my_volume_name`. You'd need to do this per volume if this is the case.

> [!IMPORTANT]
> It's vital to operation that repliqate has access to read and write from/to Docker's control surface.

> [!Tip]
> Support for TCP docker sock proxies is available if security is important: [Docker Socket Proxy][docker-socket-proxy]

## Environment Variable(s)

| Variable           | Description                                                            | Required | Default                |
|--------------------|------------------------------------------------------------------------|----------|------------------------|
| `BACKUP_ROOT_PATH` | The directory in which the backups are placed.                         | No       | `/var/repliqate`       |
| `DOCKER_SOCK_PATH` | The Docker URI. Can contain `tcp://` URI connections if required.      | No       | `/var/run/docker.sock` |
| `TZ`               | Sets the timezone of the container                                     | No       | `UTC`                  |
| `LOG_LEVEL`        | Minimum log level (Verbose, Debug, Information, Warning, Error, Fatal) | No       | `Information`          |

## Examples

### Shell
```shell
docker run -d \
  --name repliqate \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /path/to/backups:/var/repliqate \
  -v /var/lib/docker/volumes:/var/lib/docker/volumes \
  lminlone/repliqate
```

### Docker Compose

#### Basic
```yml
services:
  repliqate:
    image: lminlone/repliqate
    container_name: repliqate
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - /path/to/backups:/var/repliqate
      - /var/lib/docker/volumes:/var/lib/docker/volumes
```

#### Backup to NFS
```yml
services:
  repliqate:
    image: lminlone/repliqate
    container_name: repliqate
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - backups:/var/repliqate
      - /var/lib/docker/volumes:/var/lib/docker/volumes

volumes:
  backups:
    driver: local
    driver_opts:
      type: nfs
      o: addr=your-nas-hostname-or-ip,nolock,soft,rw
      device: :/volume/backups
```

[docker-socket-proxy]: https://github.com/linuxserver/docker-socket-proxy