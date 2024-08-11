import fs from 'node:fs'
import { PATH } from './config'

export class Notified {
  public static isFirst(): boolean {
    const path = PATH.notified
    return !fs.existsSync(path)
  }

  public static isNotified(appId: number, price: number): boolean {
    const path = PATH.notified
    const json: Record<number, number> = fs.existsSync(path)
      ? JSON.parse(fs.readFileSync(path, 'utf8'))
      : {}
    return json[appId] === price
  }

  public static getAppIds(): number[] {
    const path = PATH.notified
    const json: Record<number, number> = fs.existsSync(path)
      ? JSON.parse(fs.readFileSync(path, 'utf8'))
      : {}
    return Object.keys(json).map(Number)
  }

  public static setNotified(appId: number, price: number): void {
    const path = PATH.notified
    const json: Record<number, number> = fs.existsSync(path)
      ? JSON.parse(fs.readFileSync(path, 'utf8'))
      : {}
    json[appId] = price
    fs.writeFileSync(path, JSON.stringify(json))
  }

  public static removeNotified(appId: number): void {
    const path = PATH.notified
    const json: Record<number, number> = fs.existsSync(path)
      ? JSON.parse(fs.readFileSync(path, 'utf8'))
      : {}
    if (!json[appId]) return
    // eslint-disable-next-line @typescript-eslint/no-dynamic-delete
    delete json[appId]
    fs.writeFileSync(path, JSON.stringify(json))
  }
}
