---
title: repliqate
layout: hextra-home
---

<div class="hx:mb-12 hx:flex hx:justify-center hx:items-center hx:w-full">
    <img src="heading.png" />
</div>

<div class="hx:mb-12 content-center hx:text-center">
    {{< hextra/hero-subtitle >}}A modular backup solution designed for Docker environments, safely handling containerized workloads by stopping and restarting containers during backup operations, ensuring data consistency.{{< /hextra/hero-subtitle >}}
</div>

<div class="hx:mb-6 hx:flex hx:justify-center hx:items-center hx:w-full">
{{< hextra/hero-button text="Get Started" link="docs" >}}
</div>

<div class="hx:mt-6"></div>

{{< hextra/feature-grid >}}
    {{< hextra/feature-card
        title="Label-Based Configuration"
        subtitle="Configure backup rules using Docker container and volume labels, keeping configuration alongside the resources it protects."
        class="hx:aspect-auto hx:md:aspect-[1.1/1] hx:max-md:min-h-[340px]"
        image="images/hextra-doc.webp"
        imageClass="hx:top-[40%] hx:left-[24px] hx:w-[180%] hx:sm:w-[110%] hx:dark:opacity-80"
        style="background: radial-gradient(ellipse at 50% 80%,rgba(194,97,254,0.15),hsla(0,0%,100%,0));"
    >}}
    {{< hextra/feature-card
        title="Self-Hostable"
        subtitle="Deployable as a Docker container for integration into existing infrastructure."
        class="hx:aspect-auto hx:md:aspect-[1.1/1] hx:max-lg:min-h-[340px]"
        image="images/hextra-markdown.webp"
        imageClass="hx:top-[40%] hx:left-[36px] hx:w-[180%] hx:sm:w-[110%] hx:dark:opacity-80"
        style="background: radial-gradient(ellipse at 50% 80%,rgba(142,53,74,0.15),hsla(0,0%,100%,0));"
    >}}
    {{< hextra/feature-card
        title="Container-Safe"
        subtitle="Automatically stops and restarts containers around backup operations."
        class="hx:aspect-auto hx:md:aspect-[1.1/1] hx:max-md:min-h-[340px]"
        image="images/hextra-search.webp"
        imageClass="hx:top-[40%] hx:left-[36px] hx:w-[110%] hx:sm:w-[110%] hx:dark:opacity-80"
        style="background: radial-gradient(ellipse at 50% 80%,rgba(221,210,59,0.15),hsla(0,0%,100%,0));"
    >}}
    {{< hextra/feature-card
        title="Versioned Backups"
        subtitle="Maintains version history of backups with configurable retention policies, allowing recovery from multiple points in time."
    >}}
    {{< hextra/feature-card
        title="Intuitive Scheduling"
        subtitle="Simple, human-readable schedule configuration with convenient shortcuts like `@daily 3am` or `@weekly 4am Mon`, while still supporting traditional cron expressions for advanced use cases."
    >}}
{{< /hextra/feature-grid >}}