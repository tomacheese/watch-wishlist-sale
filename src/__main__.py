import json
import os
import re
from datetime import datetime, timezone

import requests

from src import config, get_before_price, get_lowest_price_from_steamdb, logger, send_to_discord, set_before_price
from src.SteamGame import SteamGame


def main():
    response = requests.get("https://store.steampowered.com/wishlist/id/book000/")
    response.raise_for_status()

    m = re.search(r"var g_rgWishlistData = \[(.*)];", response.text)
    if m is None:
        logger.critical("g_rgWishlistData not found.")
        exit(1)

    items = json.loads("[" + m.group(1) + "]")
    appids = [item["appid"] for item in items]

    if os.path.exists("appids.json"):
        with open("appids.json", "r") as f:
            appids.extend(json.load(f))

    fields = []
    for appid in appids:
        steam_game = SteamGame(appid)

        if steam_game.is_unreleased():
            continue  # 未発売

        logger.info(
            steam_game.get_game_name(),
            str(steam_game.get_currency_price()) + "(" + steam_game.get_currency_name() + ")",
            str(steam_game.get_discount_percent()) + "%"
        )

        if steam_game.get_discount_percent() == 0:
            logger.info("-> 未割引")
            continue  # 未割引

        if get_before_price(appid) == steam_game.get_currency_price():
            logger.info("-> 前回のチェックと変わってない")
            continue  # 前回のチェックと変わってない

        min_discount_price, min_discount_time = get_lowest_price_from_steamdb(appid)

        fields.append({
            "name": steam_game.get_game_name() + " -" + str(steam_game.get_discount_percent()) + "%",
            "value":
                "{}円 -> {}円[{}] (最安値: {}円)\n"
                "https://steamdb.info/app/{appid}/\n"
                "https://store.steampowered.com/app/{appid}/".format(
                    steam_game.get_initial_price(),
                    steam_game.get_currency_price(),
                    steam_game.get_currency_name(),
                    min_discount_price,
                    appid=appid),
            "inline": False
        })
        set_before_price(appid, steam_game.get_currency_price())

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
