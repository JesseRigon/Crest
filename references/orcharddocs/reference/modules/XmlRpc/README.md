# XML-RPC (`Grove.XmlRpc`)

The `Grove.XmlRpc` module adds support for the XML-RPC protocol, allowing client applications such as [Open Live Writer](http://openlivewriter.com/) to interact with the site.

The module provides two features:

| Feature                       | Description                                                                 |
|-------------------------------|-----------------------------------------------------------------------------|
| `Grove.XmlRpc`          | Provides the core XML-RPC endpoint and protocol support.                    |
| `Grove.RemotePublishing` | Adds the MetaWeblog API on top of XML-RPC, enabling remote creation and editing of content. Depends on `Grove.XmlRpc`. |

## Endpoints

| Route                | Purpose                                  |
|----------------------|------------------------------------------|
| `/xmlrpc`            | The XML-RPC entry point.                 |
| `/xmlrpc/metaweblog` | The MetaWeblog API endpoint (requires the Remote Publishing feature). |

## Usage

To publish from a desktop blogging client:

1. Enable the `Remote Publishing` feature (it enables `XML-RPC` automatically).
2. In the client (for example Open Live Writer), configure a new account pointing at your site, using the MetaWeblog API and the `/xmlrpc/metaweblog` endpoint.
3. Authenticate with a site user that has the permissions required to create and publish the relevant content.

The MetaWeblog API supports the typical operations used by blogging clients: retrieving recent posts, creating and editing posts, and uploading media.
