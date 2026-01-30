# GEMINI.md

## 目的
Gemini CLI 向けのコンテキストと作業方針を定義します。

## 出力スタイル
- 言語: 日本語
- トーン: プロフェッショナルかつ簡潔（CLI 向け）
- 形式: GitHub Flavored Markdown

## 共通ルール
- 会話は日本語で行う。
- コミットメッセージは Conventional Commits に従う（description は日本語）。
- 日本語と英数字の間には半角スペースを入れる。

## プロジェクト概要
- 目的: Steam ウィッシュリストの割引情報を Discord に通知する。
- 主な機能: Steam API 連携、SteamDB スクレイピング、Discord Webhook/Bot 通知。

## コーディング規約
- 言語: TypeScript
- フォーマット: Prettier
- リンター: ESLint
- コメント: 日本語（JSDoc 必須）
- エラーメッセージ: 英語

## 開発コマンド
```bash
# 依存関係のインストール
pnpm install

# 実行
pnpm start

# 開発
pnpm dev

# Lint / 型チェック
pnpm lint

# 自動修正
pnpm fix

# JSON スキーマ更新
pnpm generate-schema
```

## 注意事項
- 認証情報（Steam ID, Discord Token/Webhook）を Git にコミットしない。
- 既存のコードスタイル（`@book000/node-utils` の利用など）を優先する。
- `data/` ディレクトリは実行時のデータを保持するため、Git 管理から除外されている。

## リポジトリ固有
- `puppeteer-core` を使用しているため、環境によってはブラウザのパス設定などが必要になる場合がある。
- `schema/Configuration.json` は `src/config.ts` の `ConfigInterface` から自動生成される。
