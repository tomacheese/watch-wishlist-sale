import { Configuration, PATH } from './config'
import fs from 'node:fs'
import { getApps, WishlistItem } from './steam'
import axios from 'axios'
import { Notified } from './notified'
import { getLowestPrice } from './steamdb'
import { Discord, DiscordEmbedField, Logger } from '@book000/node-utils'

function getProfileId(config: Configuration): string {
  if (process.env.STEAM_PROFILE_ID) {
    return process.env.STEAM_PROFILE_ID
  }

  const steamConfig = config.get('steam')
  if (steamConfig?.profile_id) {
    return steamConfig.profile_id
  }

  throw new Error('STEAM_PROFILE_ID or config:steam.profile_id is required')
}

async function getWishlistAppIds(profileId: string): Promise<number[]> {
  const url = `https://store.steampowered.com/wishlist/id/${profileId}/`
  const response = await axios.get(url)
  const rgWishlistData = getRgWishlistData(response.data)
  return rgWishlistData.map((item) => item.appid)
}

function getRgWishlistData(html: string): WishlistItem[] {
  const rgWishlistData = /var g_rgWishlistData = \[(.+)];/.exec(html)?.[1]
  if (!rgWishlistData) {
    throw new Error('Failed to get g_rgWishlistData')
  }
  return JSON.parse(`[${rgWishlistData}]`) as WishlistItem[]
}

async function main() {
  const logger = Logger.configure('main')
  const config = new Configuration('data/config.json')
  config.load()
  if (!config.validate()) {
    logger.error('❌ Configuration is invalid')
    logger.error(
      `💡 Missing check(s): ${config.getValidateFailures().join(', ')}`
    )
    return
  }
  const profileId = getProfileId(config)
  const isFirst = Notified.isFirst()

  logger.info(`🆔 Steam Profile ID: ${profileId}`)

  const discordConfig = config.get('discord')
  const discord = discordConfig.webhook_url
    ? new Discord({
        webhookUrl: discordConfig.webhook_url,
      })
    : discordConfig.token && discordConfig.channel_id
      ? new Discord({
          token: discordConfig.token,
          channelId: discordConfig.channel_id,
        })
      : null
  if (discord === null) {
    logger.error('❌ Discord configuration is invalid')
    return
  }

  const appIds = await getWishlistAppIds(profileId)
  if (fs.existsSync(PATH.appIds)) {
    appIds.push(...JSON.parse(fs.readFileSync(PATH.appIds, 'utf8')))
  }

  logger.info(`🎯 Targeted apps: ${appIds.length}`)

  const apps = await getApps(appIds)
  /** 販売中 & 割引中 & 未通知のアプリ */
  const saleApps = apps.filter((app) => {
    if (!app.price_overview) {
      // 価格がない => 未発売
      return false
    }
    const discountPercent = app.price_overview.discount_percent
    if (discountPercent === 0) {
      // 割引率が 0 => 割引なし
      return false
    }
    return true
  })
  const filteredApps = saleApps.filter((app) => {
    if (!app.price_overview) {
      return false
    }
    const currencyPrice = app.price_overview.final / 100
    // 通知済み (前回の価格と同じ)
    return !Notified.isNotified(app.steam_appid, currencyPrice)
  })

  logger.info(`📦 Sale apps: ${saleApps.length}`)
  logger.info(`📦 Filtered apps: ${filteredApps.length}`)

  // SteamDB から最安値を取得
  const steamDBLowestPrices = await Promise.all(
    filteredApps.map(async (app) => {
      const lowestHistory = await getLowestPrice(app.steam_appid)
      return { appId: app.steam_appid, history: lowestHistory }
    })
  )

  // Discord に通知 (Field は 25 までという制限を忘れずに)
  // eslint-disable-next-line unicorn/no-array-reduce
  const chunkedFilteredApps = filteredApps.reduce<(typeof filteredApps)[]>(
    (accumulator, app, index) => {
      const chunkIndex = Math.floor(index / 25)
      if (!accumulator[chunkIndex]) {
        accumulator[chunkIndex] = []
      }
      accumulator[chunkIndex].push(app)
      return accumulator
    },
    []
  )
  for (const apps of chunkedFilteredApps) {
    const fields = apps
      .map((app) => {
        if (!app.price_overview) {
          return null
        }
        const currency = app.price_overview.currency
        const initialPrice = app.price_overview.initial / 100
        const currencyPrice = app.price_overview.final / 100
        const discountPercent = app.price_overview.discount_percent
        const lowestPriceHistory = steamDBLowestPrices.find(
          (lowestPrice) => lowestPrice.appId === app.steam_appid
        )
        const lowestPrice = lowestPriceHistory
          ? lowestPriceHistory.history.y
          : 'NULL'

        const urlSteamDB = `https://steamdb.info/app/${app.steam_appid}/`
        const urlSteam = `https://store.steampowered.com/app/${app.steam_appid}/`

        return {
          name: `${app.name} -${discountPercent}%`,
          value: `${initialPrice}円 -> ${currencyPrice}円[${currency}] (最安値: ${lowestPrice}円)\n${urlSteamDB}\n${urlSteam}`,
          inline: true,
        }
      })
      .filter((field) => field !== null) as DiscordEmbedField[]

    if (!isFirst) {
      await discord.sendMessage({
        embeds: [
          {
            title: 'Steam Sale Alert',
            fields,
            timestamp: new Date().toISOString(),
            color: 0xff_80_00,
          },
        ],
      })
    }

    for (const app of apps) {
      if (!app.price_overview) {
        continue
      }
      logger.info(
        `🔔 Set notified: ${app.name} (${app.steam_appid}) - ${
          app.price_overview.final / 100
        }`
      )
      const currencyPrice = app.price_overview.final / 100
      Notified.setNotified(app.steam_appid, currencyPrice)
    }
  }

  // saleApps にないアプリを Notified.removeNotified()
  const notifiedAppIds = Notified.getAppIds()
  for (const appId of notifiedAppIds) {
    if (saleApps.some((app) => app.steam_appid === appId)) {
      continue
    }
    logger.info(`❌ Remove notified: ${appId}`)
    Notified.removeNotified(appId)
  }

  logger.info(`✅ Done`)
}

;(async () => {
  const logger = Logger.configure('main')
  await main().catch((error: unknown) => {
    logger.error('❌ Error', error as Error)
    // eslint-disable-next-line unicorn/no-process-exit
    process.exit(1)
  })
})()
