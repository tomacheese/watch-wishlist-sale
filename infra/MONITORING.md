# Low-cost Azure Monitor alerting

The separate `monitoring.bicep` manages **only** Azure Monitor metric alerts. It reuses the existing Function App, Application Insights instance and notification Action Group. It makes no changes to runtime code, storage, or app settings.

## Deploy

```bash
az deployment group what-if --resource-group rg-watch-wishlist-sale --template-file infra/monitoring.bicep
az deployment group create --resource-group rg-watch-wishlist-sale --template-file infra/monitoring.bicep
```

Verify: `az monitor metrics alert list -g rg-watch-wishlist-sale --output table`

The former billable log alert is retained **disabled** for rollback: `異常検出アラート - appi-watch-wishlist-sale`.
When the replacement alerts are operating correctly, keep the old log alert disabled. To re-enable for rollback, run:

```bash
az monitor scheduled-query update --resource-group rg-watch-wishlist-sale --name '異常検出アラート - appi-watch-wishlist-sale' --disabled false
```

Re-enabling the old rule resumes its recurring charges. Disable it again after troubleshooting.

## Coverage and limitations

* Metric alerts are evaluated every 5 minutes over a rolling 15-minute window, with automatic resolution to avoid repeated email storms.
* Alerts use an aggregate **single time series** per rule, without dimensions or dynamic thresholds, to stay within the standard metric alert free allowance (subject to other alerts and billing-account rules).
* No-data and business-correctness failures are NOT detected by these metric rules. In particular, a timer function silently not running, or the Steam wishlist API returning zero items, does not trigger a failed-request metric. Use separate availability/business-outcome checks if required.
* Azure Monitor's existing Failure Anomalies smart detector and Service Health rules remain unchanged.
* Azure Cost Management invoices are delayed; inspect the next several days of billing to confirm the old scheduled query charge stopped and that metric time series fall within the free tier.
* Only the scheduled-query rule and metric alert definitions are changed. No application deployment is needed.
