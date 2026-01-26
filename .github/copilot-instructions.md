# GitHub Copilot Instructions

## プロジェクト概要
- 目的: Steam のウィッシュリストをチェックし、価格、割引率、SteamDB の最安値を Discord で通知する
- 主な機能: Steam ウィッシュリストの取得、割引情報のフィルタリング、SteamDB からの最安値取得、Discord 通知
- 対象ユーザー: Steam 利用者

## 共通ルール
- 会話は日本語で行う。
- PR とコミットは Conventional Commits に従う。
- 日本語と英数字の間には半角スペースを入れる。

## 技術スタック
- 言語: TypeScript
- 実行環境: Node.js (tsx)
- パッケージマネージャー: pnpm
- 主なライブラリ: axios, puppeteer-core, @book000/node-utils

## コーディング規約
- フォーマット: Prettier
- リンター: ESLint
- 型チェック: TypeScript (tsc)
- 関数やインターフェースには日本語で JSDoc を記載する。

## 開発コマンド
```bash
# 依存関係のインストール
pnpm install

# 実行
pnpm start

# 開発（ウォッチモード）
pnpm dev

# Lint 実行（Prettier, ESLint, TSC）
pnpm lint

# 自動修正（Prettier, ESLint）
pnpm fix

# JSON スキーマの生成
pnpm generate-schema
```

## テスト方針
- 現在、明示的なテストコードは含まれていません。
- 必要に応じて、新規機能の追加時にテストの導入を検討してください。

## セキュリティ / 機密情報
- Discord の Webhook URL やトークン、Steam プロファイル ID などの機密情報は、`data/config.json` や環境変数で管理し、絶対に Git にコミットしない。
- ログに個人情報や認証情報を出力しない。

## ドキュメント更新
- 構成の変更時には `schema/Configuration.json` を更新すること（`pnpm generate-schema` を使用）。

## リポジトリ固有
- `data/` ディレクトリ配下に設定ファイルや通知済みリストが保存される。
- `puppeteer-core` を使用して SteamDB から情報を取得しているため、ブラウザの実行環境に注意が必要。
