import { ConfigFramework } from '@book000/node-utils'

export const PATH = {
  config: process.env.CONFIG_PATH || 'data/config.json',
  appIds: process.env.APP_IDS_PATH || 'data/appIds.json',
  notified: process.env.NOTIFIED_PATH || 'data/notified.json',
}

export interface ConfigInterface {
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

export class Configuration extends ConfigFramework<ConfigInterface> {
  protected validates(): {
    [key: string]: (config: ConfigInterface) => boolean
  } {
    return {
      'discord is required': (config) => !!config.discord,
      'discord is object': (config) => typeof config.discord === 'object',
      'discord.webhook_url or discord.token and discord.channel_id is required':
        (config) =>
          !!(
            config.discord.webhook_url ||
            (config.discord.token && config.discord.channel_id)
          ),
      'discord.webhook_url is string': (config) =>
        config.discord.webhook_url === undefined ||
        typeof config.discord.webhook_url === 'string',
      'discord.token is string': (config) =>
        config.discord.token === undefined ||
        typeof config.discord.token === 'string',
      'discord.channel_id is string': (config) =>
        config.discord.channel_id === undefined ||
        typeof config.discord.channel_id === 'string',
      'steam.profile_id is string': (config) =>
        config.steam === undefined ||
        (config.steam && typeof config.steam.profile_id === 'string'),
    }
  }
}
