import datetime
import json
import logging
import os
from logging.handlers import TimedRotatingFileHandler

import requests


def get_lowest_price_from_steamdb(appid: int):
    response = requests.get(
        "https://steamdb.info/api/GetPriceHistory/?appid=" + str(appid) + "&cc=jp",
        headers={
            "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:82.0) Gecko/20100101 Firefox/82.0",
            "X-Requested-With": "XMLHttpRequest",
            "Referer": "https://steamdb.info/app/" + str(appid) + "/"
        }
    )
    if response.status_code != 200:
        return None, None

    result = response.json()
    if not result["success"]:
        return None, None

    min_discount_price = None
    min_discount_time = None
    for item in result["data"]["history"]:
        if min_discount_price is None or min_discount_price > item["y"]:
            min_discount_price = item["y"]
            min_discount_time = item["x"]

    return min_discount_price, min_discount_time


def init_logger(child_name: str = None) -> logging.Logger:
    _logger = logging.getLogger("TwitterDMMemo")
    if child_name is not None:
        _logger = _logger.getChild(child_name)
    dt = datetime.datetime.now().date()
    date_time = dt.strftime("%Y-%m-%d")

    if not os.path.exists("logs/"):
        os.mkdir("logs/")

    rotatedHandler = TimedRotatingFileHandler(
        filename="logs/%s.log" % date_time,
        encoding="UTF-8",
        when="MIDNIGHT",
        backupCount=30
    )
    rotatedHandler.setLevel(logging.INFO)
    rotatedHandler.setFormatter(logging.Formatter('[%(asctime)s] [%(name)s/%(levelname)s]: %(message)s'))
    _logger.addHandler(rotatedHandler)
    streamHandler = logging.StreamHandler()
    streamHandler.setFormatter(logging.Formatter('[%(asctime)s] [%(name)s/%(levelname)s]: %(message)s'))
    _logger.addHandler(streamHandler)

    return _logger


logger = init_logger()


def send_to_discord(token, channelId, message, embed=None, files=None):
    if files is None:
        files = {}
    headers = {
        "Authorization": "Bot {token}".format(token=token),
        "User-Agent": "Bot"
    }
    params = {
        "payload_json": json.dumps({
            "content": message,
            "embed": embed
        })
    }
    response = requests.post(
        "https://discord.com/api/channels/{channelId}/messages".format(channelId=channelId), headers=headers,
        data=params, files=files)
    print(response.status_code)
    print(response.json())


def get_before_price(appid: int):
    if not os.path.exists("before_price.json"):
        return None

    with open("before_price.json", "r") as f:
        result = json.load(f)

        if str(appid) not in result:
            return None

        return result[str(appid)]


def set_before_price(appid: int, percent: int):
    result = {}
    if os.path.exists("before_price.json"):
        with open("before_price.json", "r") as f:
            result = json.load(f)

    result[str(appid)] = percent

    with open("before_price.json", "w") as f:
        json.dump(result, f)
