import requests


class SteamGame:
    def __init__(self, appid: int):
        response = requests.get("https://store.steampowered.com/api/appdetails?appids=" + str(appid) + "&cc=JP")
        if response.status_code != 200:
            raise SteamGameNotFoundError()

        result = response.json()
        data = result[str(appid)]["data"]

        self.__gameName = data["name"]

        self.__unreleased = False
        if "price_overview" not in data:
            self.__unreleased = True
            return

        self.__initialPrice = data["price_overview"]["initial"] / 100
        self.__currencyPrice = data["price_overview"]["final"] / 100
        self.__currencyName = data["price_overview"]["currency"]
        self.__discountPercent = data["price_overview"]["discount_percent"]

    def get_game_name(self):
        """
        ゲーム名

        :return: ゲーム名
        """
        return self.__gameName

    def is_unreleased(self):
        """
        未発売か？

        :return: 未発売かどうか
        """
        return self.__unreleased

    def get_initial_price(self):
        """
        定価

        :return: 定価
        """
        return self.__initialPrice

    def get_currency_price(self):
        """
        現在価格

        :return: 現在価格
        """
        return self.__currencyPrice

    def get_currency_name(self):
        """
        価格の単位

        :return: 価格の単位 (e.g. JPY)
        """
        return self.__currencyName

    def get_discount_percent(self):
        """
        割引率

        :return: 割引率
        """
        return self.__discountPercent


class SteamGameNotFoundError(Exception):
    pass
