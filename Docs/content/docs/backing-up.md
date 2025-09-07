---
title: "Backing Up"
weight: 2
tags:
  - Docs
  - Guide
cascade:
  type: docs
---

Repliqate uses Docker labels for configuration. This keeps backup policies close to the containers and volumes they apply to, eliminating the need for separate configuration files.

## Container Labels
| Label                    | Description                                                                                                                            | Default  | Example                                                                          |
|--------------------------|----------------------------------------------------------------------------------------------------------------------------------------|----------|----------------------------------------------------------------------------------|
| `repliqate.enabled`      | Enables backup for the container                                                                                                       | `false`  | `true`                                                                           |
| `repliqate.engine`       | Backup engine selection                                                                                                                | `restic` | `restic`                                                                         |
| `repliqate.schedule`     | Backup schedule (cron format)                                                                                                          | `none`   | `@daily 3am` (see [Scheduling](#scheduling) section)                             |
| `repliqate.backup_id`    | Unique backup identifier for the container.<br/><br/>**NOTE**: Ensure this is fully unique across all containers on the docker server. | `none`   | `prod-db-01`                                                                     |
| `repliqate.retention`    | Keep all snapshots taken within the specified time span (years, months, days, hours) before the latest snapshot.                       | `N/A`    | `2y5m7d3h` keeps snapshots from the last 2 years, 5 months, 7 days, and 3 hours. |

## Volume Labels
| Label                | Description                                   | Default   |
|----------------------|-----------------------------------------------|-----------|
| `repliqate.exclude`  | Exclude this volume from container backups.   | `false`   |

## Examples
### Shell
#### Simple
```shell
docker run -d \
  --label repliqate.enabled=true \
  --label repliqate.engine=restic \
  --label repliqate.schedule="@daily 3am" \
  --label repliqate.backup_id=my_app_01 \
  --name my_app \
  my_image:latest
```

### Compose
#### Example 1
```yml
services:
  app:
    image: my-app:latest
    volumes:
      - data:/my-app/data
      - uploads:/my-app/uploads
    labels:
      repliqate.enabled: 'true'
      repliqate.schedule: "@daily 10:34"
      repliqate.engine: restic
      repliqate.backup_id: my_app

volumes:
  data:
    labels:
      repliqate.exclude: 'true' # Exclude from being backed up
  uploads:
```