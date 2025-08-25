<p align="center">
  <img src="Docs/heading.png">
</p>

Repliqate is a modular backup solution designed for Docker environments. It safely handles containerized workloads by stopping and restarting containers during backup operations, ensuring data consistency.

Currently, Repliqate integrates with Restic as its backup engine, with planned support for additional providers in the future.

# Features

- **Label-based configuration**: Configure backup rules using Docker container and volume labels, keeping configuration alongside the resources it protects.
- **Self-hostable**: Deployable as a Docker container for integration into existing infrastructure.
- **Container-safe backups**: Automatically stops and restarts containers around backup operations.

# Running Repliqate
## Prerequisites

- Docker Engine 24.0 or later
- Access to container runtime socket
- Storage location for backup data

```shell
docker run -d \
  --name repliqate \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v /path/to/backups:/app/backups \
  ghcr.io/lminlone/repliqate:latest
```

- `/var/run/docker.sock`: Required so Repliqate can read labels and control containers.
- `/app/backups`: Storage for metadata and temporary backup files.

# Environment Variables
| Variable | Description |
|----------|-------------|
| `DOCKER_SOCK_PATH` | The Docker URI. Defaults to `/var/run/docker.sock`. Can contain `tcp://` connections if required |

# Backup Configuration

Repliqate uses Docker labels for configuration. This keeps backup policies close to the containers and volumes they apply to, eliminating the need for separate configuration files.

## Container Labels
| Label | Description | Example                                              |
|-------|-------------|------------------------------------------------------|
| `repliqate.enabled` | Enables backup for the container | `true`                                               |
| `repliqate.method` | Backup engine selection | `restic`                                             |
| `repliqate.schedule` | Backup schedule (cron format) | `@daily 3am` (see [Scheduling](#scheduling) section) |
| `repliqate.backup_id` | Unique backup set identifier | `prod-db-01`                                         |

**Example: Labeling a container**
```shell
docker run -d \
  --label repliqate.enabled=true \
  --label repliqate.method=restic \
  --label repliqate.schedule="@daily 3am" \
  --label repliqate.backup_id=my_app_01 \
  --name my_app \
  my_image:latest
```

# Scheduling
Repliqate provides flexible scheduling options using a (half-custom) syntax while maintaining compatibility with "standard" cron expressions.

## Shorthand Syntax
### Frequency Options
- `@daily <time>` - Run once per day.
- `@weekly <time> <day of the week>` - Run once per week on this specific day.
- `@monthly <time> <day of the month>` - Run once per month on this specific date.

### Time Formats
Supports both 12-hour and 24-hour time formats:
- 12-hour: `3:00 PM`, `3PM`, `3:00pm`
- 24-hour: `15:00`

### Examples
- `@monthly 9am 15`: Run on the 15th of every month at 9am.
- `@weekly 4am Mon`: Run weekly on Mondays at 4am.
- `@daily 23:59`: Run every day at 11:59pm.

## Advanced Scheduling
For more complex scheduling needs, Repliqate also accepts [Quartz cron expressions](http://www.cronmaker.com)

### Examples
- `0 0 19 1/1 * ? *`: Run every hour (not recommended) starting at 7pm.
- `0 0 2 ? * MON-FRI *`: Run every weekday at 2am.

# Roadmap
- ☐ Support for additional backup engines (e.g., rclone, fully native solution, postgres DB dump, etc).
- ☐ Modular plugins to allow custom backup engines.
- ☐ Restoration options.
- ☐ Frontend UI for advanced configurations and restoration.
- ☐ Enhanced monitoring and logging options (such as for Grafana Loki or Graylog)

# Contributing
Contributions are welcome. Please open an issue to discuss proposed changes before submitting a pull request.

# License
Repliqate is licensed under the MIT License. See [LICENSE](License.md)