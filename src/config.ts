import fs from 'node:fs'

export const PATH = {
  config: process.env.CONFIG_PATH || 'data/config.json',
  appIds: process.env.APP_IDS_PATH || 'data/appIds.json',
  notified: process.env.NOTIFIED_PATH || 'data/notified.json',
}

export interface Configuration {
  /** Discord webhook URL or bot token */
  discord: {
    /** Discord webhook URL (required if using webhook) */
    webhook_url?: string
    /** Discord bot token (required if using bot) */
    token?: string
    /** Discord channel ID (required if using bot) */
    channel_id?: string
  }
  steam?: {
    /** Steam profile ID */
    profile_id: string
  }
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
const isConfig = (config: any): config is Configuration => {
  return (
    config &&
    typeof config.discord === 'object' &&
    // webhook_url があるか token と channel_id があるか
    (config.discord.webhook_url ||
      (config.discord.token && config.discord.channel_id)) &&
    // webhook_url があるとき、string である
    (config.discord.webhook_url === undefined ||
      typeof config.discord.webhook_url === 'string') &&
    // token があるとき、string である
    (config.discord.token === undefined ||
      typeof config.discord.token === 'string') &&
    // channel_id があるとき、string である
    (config.discord.channel_id === undefined ||
      typeof config.discord.channel_id === 'string') &&
    // steam がないか、あるなら profile_id がある
    (config.steam === undefined ||
      (config.steam && typeof config.steam.profile_id === 'string'))
  )
}

export function loadConfig(): Configuration {
  if (!fs.existsSync(PATH.config)) {
    throw new Error('Config file not found')
  }
  const config = JSON.parse(fs.readFileSync(PATH.config, 'utf8'))
  if (!isConfig(config)) {
    throw new Error('Invalid config')
  }
  return config
}
