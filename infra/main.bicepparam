using 'main.bicep'

param discordWebhookUrl = readEnvironmentVariable('DISCORD_WEBHOOK_URL', '')
param itadApiKey = readEnvironmentVariable('ITAD_API_KEY', '')
param steamProfileId = readEnvironmentVariable('STEAM_PROFILE_ID', '')