import { Logger } from '@book000/node-utils'
import puppeteer, {
  BrowserConnectOptions,
  BrowserLaunchArgumentOptions,
  LaunchOptions,
  Product,
  Browser,
  Page,
} from 'puppeteer-core'

export interface History {
  /** セール番号 */
  x: number
  /** 価格 */
  y: number
  /** 価格（単位付き） */
  f: string
  /** 割引率 */
  d: number
}

export type Sales = Record<number, string>

export interface PriceHistoryData {
  /** 価格履歴 */
  history: History[]
  /** セール履歴 */
  sales: Sales
}

export interface SteamDBPriceHistoryResponse {
  success: boolean
  data: PriceHistoryData
}

export class SteamDB {
  private static browser: Browser

  // eslint-disable-next-line @typescript-eslint/no-empty-function
  private constructor() {}

  public static async getInstance() {
    const puppeteerOptions: LaunchOptions &
      BrowserLaunchArgumentOptions &
      BrowserConnectOptions & {
        product?: Product
        extraPrefsFirefox?: Record<string, unknown>
      } = {
      headless: false,
      executablePath: '/usr/bin/chromium-browser',
      args: [
        '--no-sandbox',
        '--disable-setuid-sandbox',
        '--disable-dev-shm-usage',
        '--disable-accelerated-2d-canvas',
        '--no-first-run',
        '--no-zygote',
        '--disable-gpu',
        '--lang=ja',
        '--window-size=600,800',
      ],
    }
    this.browser = await puppeteer.launch(puppeteerOptions)

    return new SteamDB()
  }

  public async getPriceHistory(appId: number) {
    const page = await SteamDB.browser.newPage()
    page.setDefaultNavigationTimeout(120 * 1000)
    await page.evaluateOnNewDocument(() => {
      // eslint-disable-next-line @typescript-eslint/no-empty-function
      Object.defineProperty(navigator, 'webdriver', () => {})
      // @ts-expect-error __proto__ is not defined in types
      // eslint-disable-next-line no-proto, @typescript-eslint/no-unsafe-member-access
      delete navigator.__proto__.webdriver
    })
    await page.goto(`https://steamdb.info/app/${appId}/`, {
      waitUntil: 'networkidle2',
    })

    const priceHistoryPromise =
      this.getApiResponse<SteamDBPriceHistoryResponse>(
        page,
        'GET',
        'https://steamdb.info/api/GetPriceHistory/'
      )
    await this.scrollToBottom(page)
    const priceHistory = await priceHistoryPromise
    await page.close()

    if (!priceHistory.success) {
      throw new Error('Failed to get price history')
    }

    return priceHistory.data
  }

  public async close() {
    await SteamDB.browser.close()
  }

  private async scrollToBottom(page: Page) {
    await page.evaluate(() => {
      window.scrollTo({
        top: document.body.scrollHeight,
        behavior: 'smooth',
      })
    })
  }

  private getApiResponse<T>(page: Page, method: string, url: string) {
    const logger = Logger.configure('SteamDB:getApiResponse')
    return new Promise<T>((resolve, reject) => {
      const emitter = page.on('requestfinished', (request) => {
        const response = request.response()
        if (!response) {
          return
        }
        const requestUrl = response.url()
        if (!requestUrl.includes(url)) {
          return
        }
        const requestMethod = response.request().method()
        logger.info(requestMethod + ' ' + requestUrl)
        if (requestMethod.toUpperCase() !== method.toUpperCase()) {
          return
        }
        response
          .json()
          .then((json) => {
            emitter.removeAllListeners()
            resolve(json)
          })
          .catch((error: unknown) => {
            emitter.removeAllListeners()
            reject(error as Error)
          })
      })
    })
  }
}

export async function getLowestPrice(appId: number) {
  const steamDB = await SteamDB.getInstance()
  const priceHistory = await steamDB.getPriceHistory(appId)
  await steamDB.close()
  // eslint-disable-next-line unicorn/no-array-reduce
  const lowestPrice = priceHistory.history.reduce((previous, current) => {
    return previous.y < current.y ? previous : current
  })
  return lowestPrice
}
