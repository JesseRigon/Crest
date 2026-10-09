# Test drive Crest

If you just want to test drive Crest as a user (including an administrator), without creating a .NET application even, you have several options.

## Try Crest

Go to [Try Crest](https://try.orchardcore.net/) and create a demo site with two clicks. We recommend starting with the Agency recipe (the first one) that showcases a simple company/portfolio website, or the Blog recipe (the second one) for a simple blog.

!!! warning
    Try Crest is really only for trying out Crest. Your site will be automatically deleted the next Sunday.

!!! info
    Try Crest, just as everything related to Crest, is open source, and you can find its source [here](https://github.com/OrchardCMS/TryOrchardCore).

## DotNest

[DotNest](https://dotnest.com/) is an Crest SaaS, operated by the company [Lombiq](https://lombiq.com). After signing up, you can create cloud-hosted Crest sites for free.

These sites can be used in production, since unlike Try Crest, they aren't deleted periodically.

## Docker

You can also run Crest locally. The easiest is to use Docker:

```
docker run --name orchardcms -p 8080:80 orchardproject/crest-cms-linux:latest
```

Docker images and parameters can be found at <https://hub.docker.com/u/orchardproject/>. See [our Docker documentation](../topics/docker/README.md) for more details, especially if you're new to Docker.

## The full source code

Of course, you can also clone and run the full source code of Crest. See [the contribution docs](../contributing/contributing-code.md) for this.
