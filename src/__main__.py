import json
import re

import requests

from src import get_lowest_price_from_steamdb, send_to_discord, config, get_before_price, set_before_price, logger
from src.SteamGame import SteamGame
from datetime import datetime, tzinfo, timezone


def main():
    response = requests.get("https://store.steampowered.com/wishlist/id/book000/")
    response.raise_for_status()

    m = re.search(r"var g_rgWishlistData = \[(.*)];", response.text)
    if m is None:
        logger.critical("g_rgWishlistData not found.")
        exit(1)

    fields = []
    items = json.loads("[" + m.group(1) + "]")
    for item in items:
        steamGame = SteamGame(item["appid"])

        if steamGame.is_unreleased():
            continue  # 未発売

        logger.info(
            steamGame.get_game_name(),
            str(steamGame.get_currency_price()) + "(" + steamGame.get_currency_name() + ")",
            str(steamGame.get_discount_percent()) + "%"
        )

        if steamGame.get_discount_percent() == 0:
            logger.info("-> 未割引")
            continue  # 未割引

        if get_before_price(item["appid"]) == steamGame.get_currency_price():
            logger.info("-> 前回のチェックと変わってない")
            continue  # 前回のチェックと変わってない

        min_discount_price, min_discount_time = get_lowest_price_from_steamdb(item["appid"])

        fields.append({
            "name": steamGame.get_game_name() + " -" + str(steamGame.get_discount_percent()) + "%",
            "value":
                "{}円 -> {}円[{}] (最安値: {}円)\n"
                "https://steamdb.info/app/{appid}/\n"
                "https://store.steampowered.com/app/{appid}/".format(
                    steamGame.get_initial_price(),
                    steamGame.get_currency_price(),
                    steamGame.get_currency_name(),
                    min_discount_price,
                    appid=item["appid"]
                ),
            "inline": False
        })
        set_before_price(item["appid"], steamGame.get_currency_price())

    if len(fields) == 0:
        logger.info("len(fields) == 0")
        return

    embed = {
        "title": "Steam Sale Alert",
        "url": "https://store.steampowered.com/wishlist/id/book000/",
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "color": 0xff8000,
        "fields": fields
    }

    send_to_discord(config.DISCORD_TOKEN, config.DISCORD_CHANNEL_ID, "", embed)


if __name__ == "__main__":
    main()
