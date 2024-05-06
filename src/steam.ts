import axios from 'axios'

export interface WishlistItem {
  appid: number
  priority: number
  added: number
}

export interface PcRequirements {
  minimum: string
  recommended: string
}

export interface PriceOverview {
  currency: string
  initial: number
  final: number
  discount_percent: number
  initial_formatted: string
  final_formatted: string
}

export interface Sub {
  packageid: number
  percent_savings_text: string
  percent_savings: number
  option_text: string
  option_description: string
  can_get_free_license: string
  is_free_license: boolean
  price_in_cents_with_discount: number
}

export interface PackageGroup {
  name: string
  title: string
  description: string
  selection_text: string
  save_text: string
  display_type: unknown
  is_recurring_subscription: string
  subs: Sub[]
}

export interface Platforms {
  windows: boolean
  mac: boolean
  linux: boolean
}

export interface Metacritic {
  score: number
  url: string
}

export interface Category {
  id: number
  description: string
}

export interface Genre {
  id: string
  description: string
}

export interface Screenshot {
  id: number
  path_thumbnail: string
  path_full: string
}

export interface Webm {
  480: string
  max: string
}

export interface Mp4 {
  480: string
  max: string
}

export interface Movie {
  id: number
  name: string
  thumbnail: string
  webm: Webm
  mp4: Mp4
  highlight: boolean
}

export interface Recommendations {
  total: number
}

export interface Highlighted {
  name: string
  path: string
}

export interface Achievements {
  total: number
  highlighted: Highlighted[]
}

export interface ReleaseDate {
  coming_soon: boolean
  date: string
}

export interface SupportInfo {
  url: string
  email: string
}

export interface ContentDescriptors {
  ids: number[]
  notes: string
}

export interface Demo {
  appid: number
  description: string
}

export interface AppData {
  type: string
  name: string
  steam_appid: number
  required_age: unknown
  is_free: boolean
  controller_support: string
  dlc: number[]
  detailed_description: string
  about_the_game: string
  short_description: string
  supported_languages: string
  header_image: string
  website: string
  pc_requirements: PcRequirements
  mac_requirements: unknown
  linux_requirements: unknown
  legal_notice: string
  developers: string[]
  publishers: string[]
  price_overview?: PriceOverview
  packages: number[]
  package_groups: PackageGroup[]
  platforms: Platforms
  metacritic: Metacritic
  categories: Category[]
  genres: Genre[]
  screenshots: Screenshot[]
  movies: Movie[]
  recommendations: Recommendations
  achievements: Achievements
  release_date: ReleaseDate
  support_info: SupportInfo
  background: string
  background_raw: string
  content_descriptors: ContentDescriptors
  reviews: string
  drm_notice: string
  demos: Demo[]
  ext_user_account_notice: string
}

export interface AppResult {
  success: boolean
  data: AppData
}

export type AppResponse = Record<number, AppResult>

export async function getApp(appId: number): Promise<AppData> {
  const url = `https://store.steampowered.com/api/appdetails?appids=${appId}&cc=JP`
  const response = await axios.get<AppResponse>(url)
  if (!response.data[appId].success) {
    throw new Error(`Failed to get app data for app id ${appId}`)
  }
  return response.data[appId].data
}

export async function getApps(appIds: number[]): Promise<AppData[]> {
  const promises = appIds.map((appId) => getApp(appId))
  const apps = await Promise.all(promises)
  return apps
}
