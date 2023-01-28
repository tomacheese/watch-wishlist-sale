# WatchWishlistSale

Check your Steam wishlist and notify you via Discord of the price, discount rate, and the lowest price in SteamDB.

## Installation

Works in Docker (Compose) environment.

### Docker

If you want to use Docker, write the following in `docker-compose.yml`:

```yaml
version: '3.8'
services:
  app:
    image: ghcr.io/tomacheese/watch-wishlist-sale:latest
    volumes:
      - type: bind
        source: ./data
        target: /data/
    init: true
    restart: always
```

After that, you can start it with `docker-compose up -d` after creating a configuration file with reference to [Configuration section](#configuration).

## Configuration

The configuration file `data/config.json` is used by default.  
If the environment variable `CONFIG_FILE` is set, the specified value is taken as the path to the configuration file.

See here for the JSON Schema of the configuration file: [schema/Configuration.json](schema/Configuration.json)

```json
{
  "$schema": "https://raw.githubusercontent.com/tomacheese/watch-wishlist-sale/master/schema/Configuration.json"
}
```

## License

The license for this project is [MIT License](LICENSE).
