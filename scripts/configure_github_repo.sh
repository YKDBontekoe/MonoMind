#!/usr/bin/env bash
# Configure MonoMind GitHub Actions permissions and main-branch rulesets.
# Requires: gh auth with repo admin access.
set -euo pipefail

REPO="${1:-YKDBontekoe/MonoMind}"
RULESET_ID_PROTECT_MAIN=17656282
RULESET_ID_DUPLICATE_MAIN=17729013
# github-actions[bot] user id; allows release workflow to push VERSION/CHANGELOG to main.
GITHUB_ACTIONS_BOT_USER_ID=41898282

echo "==> Updating workflow permissions for ${REPO}"
gh api --method PUT "repos/${REPO}/actions/permissions/workflow" --input - <<'EOF'
{
  "default_workflow_permissions": "read",
  "can_approve_pull_request_reviews": true
}
EOF

echo "==> Updating protect-main ruleset (${RULESET_ID_PROTECT_MAIN})"
gh api --method PUT "repos/${REPO}/rulesets/${RULESET_ID_PROTECT_MAIN}" --input - <<'EOF'
{
  "name": "protect-main",
  "target": "branch",
  "enforcement": "active",
  "conditions": {
    "ref_name": {
      "exclude": [],
      "include": ["~DEFAULT_BRANCH"]
    }
  },
  "bypass_actors": [
    {
      "actor_id": 41898282,
      "actor_type": "User",
      "bypass_mode": "always"
    }
  ],
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" },
    {
      "type": "pull_request",
      "parameters": {
        "required_approving_review_count": 0,
        "dismiss_stale_reviews_on_push": false,
        "required_reviewers": [],
        "require_code_owner_review": false,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false,
        "allowed_merge_methods": ["merge", "squash", "rebase"]
      }
    },
    {
      "type": "required_status_checks",
      "parameters": {
        "strict_required_status_checks_policy": false,
        "required_status_checks": [
          { "context": "Build (ubuntu-latest)" },
          { "context": "Unit tests (ubuntu-latest)" },
          { "context": "Integration tests (ubuntu-latest)" },
          { "context": "Atlas validation" },
          { "context": "dotnet format" }
        ]
      }
    }
  ]
}
EOF

if gh api "repos/${REPO}/rulesets/${RULESET_ID_DUPLICATE_MAIN}" >/dev/null 2>&1; then
  echo "==> Deleting duplicate disabled ruleset (${RULESET_ID_DUPLICATE_MAIN})"
  gh api --method DELETE "repos/${REPO}/rulesets/${RULESET_ID_DUPLICATE_MAIN}"
fi

echo "==> Current workflow permissions"
gh api "repos/${REPO}/actions/permissions/workflow"

echo "==> Active rules for main"
gh ruleset check main -R "${REPO}"

echo "Done."
