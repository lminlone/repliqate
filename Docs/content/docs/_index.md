---
linkTitle: "Documentation"
title: Introduction
cascade:
  type: docs
---

## What is repliqate?

Repliqate is a modular backup solution designed for [Docker][docker] environments. It safely handles containerized workloads by stopping and restarting containers during backup operations, ensuring data consistency.

Currently, Repliqate integrates with Restic as its backup engine, with planned support for additional providers in the future.

## Features

- **Label-based configuration**: Configure backup rules using Docker container **and volume** labels, keeping configuration alongside the resources it protects.
- **Self-hostable**: Deployable as a Docker container for integration into existing infrastructure.
- **Container-safe**: Automatically stops and restarts containers around backup operations.
- **Versioned backups**: Maintains version history of backups with configurable retention policies, allowing recovery from multiple points in time.
- **Intuitive scheduling**: Simple, human-readable schedule configuration with convenient shortcuts like `@daily 3am` or `@weekly 4am Mon`, while still supporting traditional cron expressions for advanced use cases.

## Questions or Feedback?

{{< callout emoji="❓" >}}
repliqate is still in active development.
Have a question or feedback? Feel free to [open an issue](https://github.com/lminlone/repliqate/issues)!
{{< /callout >}}

[docker]: https://www.docker.com/