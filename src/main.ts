import { loadConfig, PATH } from './config'
import fs from 'node:fs'
import { getApps, WishlistItem } from './steam'
import axios from 'axios'
import { Notified } from './notified'
import { getLowestPrice } from './steamdb'
import { sendDiscordMessage } from './discord'

function getProfileId() {
  if (process.env.STEAM_PROFILE_ID) {
    return process.env.STEAM_PROFILE_ID
  }

  if (fs.existsSync(PATH.config)) {
    const config = JSON.parse(fs.readFileSync(PATH.config, 'utf8'))
    if (config.steam && config.steam.profile_id) {
      return config.steam.profile_id
    }
  }

  throw new Error('STEAM_PROFILE_ID or STEAM_CUSTOM_URL_ID is required')
}

async function getWishlistAppIds(profileId: string): Promise<number[]> {
  const url = `https://store.steampowered.com/wishlist/id/${profileId}/`
  const response = await axios.get(url)
  const rgWishlistData = getRgWishlistData(response.data)
  return rgWishlistData.map((item) => item.appid)
}

function getRgWishlistData(html: string): WishlistItem[] {
  const rgWishlistData = html.match(/var g_rgWishlistData = \[(.+)];/)?.[1]
  if (!rgWishlistData) {
    throw new Error('Failed to get g_rgWishlistData')
  }
  return JSON.parse(`[${rgWishlistData}]`) as WishlistItem[]
}

// eslint-disable-next-line @typescript-eslint/no-unused-vars, @typescript-eslint/ban-ts-comment
// @ts-ignore
// eslint-disable-next-line @typescript-eslint/no-unused-vars
async function main() {
  const config = loadConfig()
  const profileId = getProfileId()
  const isFirst = Notified.isFirst()

  console.log(`🆔 Steam Profile ID: ${profileId}`)

  const appIds = await getWishlistAppIds(profileId)
  if (fs.existsSync(PATH.appIds)) {
    appIds.push(...JSON.parse(fs.readFileSync(PATH.appIds, 'utf8')))
  }

  console.log(`🎯 Targeted apps: ${appIds.length}`)

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
    const currencyPrice = app.price_overview.final / 100
    // 通知済み (前回の価格と同じ)
    return !Notified.isNotified(app.steam_appid, currencyPrice)
  })

  console.log(`📦 Sale apps: ${saleApps.length}`)
  console.log(`📦 Filtered apps: ${filteredApps.length}`)

  // SteamDB から最安値を取得
  const steamDBLowestPrices = await Promise.all(
    filteredApps.map(async (app) => {
      const lowestHistory = await getLowestPrice(app.steam_appid)
      return { appId: app.steam_appid, history: lowestHistory }
    })
  )

  // Discord に通知 (Field は 25 までという制限を忘れずに)
  const chunkedFilteredApps = filteredApps.reduce((accumulator, app, index) => {
    const chunkIndex = Math.floor(index / 25)
    if (!accumulator[chunkIndex]) {
      accumulator[chunkIndex] = []
    }
    accumulator[chunkIndex].push(app)
    return accumulator
  }, [] as (typeof filteredApps)[])
  for (const apps of chunkedFilteredApps) {
    const fields = apps.map((app) => {
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

    if (!isFirst) {
      sendDiscordMessage(config, '', {
        title: 'Steam Sale Alert',
        fields,
        timestamp: new Date().toISOString(),
        color: 0xff_80_00,
      })
    }

    for (const app of apps) {
      console.log(
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
    console.log(`❌ Remove notified: ${appId}`)
    Notified.removeNotified(appId)
  }

  console.log(`✅ Done`)
}

;(async () => {
  await main().catch((error) => {
    console.error(error)
    // eslint-disable-next-line unicorn/no-process-exit
    process.exit(1)
  })
})()
