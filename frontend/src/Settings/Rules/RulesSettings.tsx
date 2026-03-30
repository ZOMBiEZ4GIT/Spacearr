import React, { useCallback, useEffect, useState } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import RuleEditor from './RuleEditor';
import styles from './RulesSettings.css';

type RuleAction = 'enforce' | 'flag' | 'delete';
type RuleSource = 'any' | 'radarr' | 'sonarr';

export interface Rule {
  id: number;
  name: string;
  source: RuleSource;
  minSize: number | null;
  maxQuality: string | null;
  tags: string[];
  action: RuleAction;
  targetQuality: string | null;
  active: boolean;
}

const ACTION_LABELS: Record<RuleAction, string> = {
  enforce: 'Enforce Max Quality',
  flag: 'Flag for Review',
  delete: 'Auto Delete',
};

const ACTION_STYLES: Record<RuleAction, string> = {
  enforce: 'actionEnforce',
  flag: 'actionFlag',
  delete: 'actionDelete',
};

// Mock data for development
const MOCK_RULES: Rule[] = [
  {
    id: 1,
    name: 'Limit Movies to 1080p',
    source: 'radarr',
    minSize: 20_000_000_000,
    maxQuality: 'Bluray-1080p',
    tags: [],
    action: 'enforce',
    targetQuality: 'Bluray-1080p',
    active: true,
  },
  {
    id: 2,
    name: 'Flag Large TV Episodes',
    source: 'sonarr',
    minSize: 8_000_000_000,
    maxQuality: null,
    tags: ['hdr'],
    action: 'flag',
    targetQuality: null,
    active: true,
  },
  {
    id: 3,
    name: 'Remove Unmonitored Remux',
    source: 'any',
    minSize: null,
    maxQuality: 'Remux-2160p',
    tags: [],
    action: 'delete',
    targetQuality: null,
    active: false,
  },
];

function buildSummary(rule: Rule): string {
  const parts: string[] = [];

  if (rule.source !== 'any') {
    parts.push(`Source: ${rule.source === 'radarr' ? 'Radarr' : 'Sonarr'}`);
  }

  if (rule.minSize != null) {
    const gb = (rule.minSize / 1_000_000_000).toFixed(0);
    parts.push(`Min size: ${gb} GB`);
  }

  if (rule.maxQuality) {
    parts.push(`Max quality: ${rule.maxQuality}`);
  }

  if (rule.tags.length > 0) {
    parts.push(`Tags: ${rule.tags.join(', ')}`);
  }

  return parts.length > 0 ? parts.join(' | ') : 'No filters';
}

function RulesSettings() {
  const [rules, setRules] = useState<Rule[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<Rule | null>(null);

  useEffect(() => {
    // GET /api/v3/rule
    fetch('/api/v3/rule')
      .then((res) => res.json())
      .then((data) => {
        setRules(data);
        setIsLoading(false);
      })
      .catch(() => {
        // Fall back to mock data during development
        setRules(MOCK_RULES);
        setIsLoading(false);
      });
  }, []);

  const handleAdd = useCallback(() => {
    setEditingRule(null);
    setEditorOpen(true);
  }, []);

  const handleEdit = useCallback((rule: Rule) => {
    setEditingRule(rule);
    setEditorOpen(true);
  }, []);

  const handleEditorClose = useCallback(() => {
    setEditorOpen(false);
    setEditingRule(null);
  }, []);

  const handleEditorSave = useCallback(
    (rule: Rule) => {
      if (editingRule) {
        setRules((prev) =>
          prev.map((r) => (r.id === rule.id ? rule : r))
        );
      } else {
        setRules((prev) => [...prev, { ...rule, id: Date.now() }]);
      }

      setEditorOpen(false);
      setEditingRule(null);
    },
    [editingRule]
  );

  const handleToggle = useCallback((id: number) => {
    setRules((prev) =>
      prev.map((r) => (r.id === id ? { ...r, active: !r.active } : r))
    );
  }, []);

  const handleDelete = useCallback((id: number) => {
    setRules((prev) => prev.filter((r) => r.id !== id));
  }, []);

  if (isLoading) {
    return (
      <PageContent title="Rules">
        <PageContentBody>
          <div className={styles.loading}>Loading rules...</div>
        </PageContentBody>
      </PageContent>
    );
  }

  return (
    <PageContent title="Rules">
      <PageContentBody>
        <div className={styles.toolbar}>
          <div />

          <button className={styles.addButton} onClick={handleAdd}>
            Add Rule
          </button>
        </div>

        {rules.length === 0 ? (
          <div className={styles.emptyState}>
            <div>No rules configured</div>
            <div className={styles.emptyHint}>
              Add a rule to automate space management.
            </div>
          </div>
        ) : (
          <div className={styles.rulesList}>
            {rules.map((rule) => (
              <div key={rule.id} className={styles.ruleCard}>
                <div className={styles.ruleInfo}>
                  <div className={styles.ruleName}>
                    <span
                      className={styles[ACTION_STYLES[rule.action]]}
                    >
                      {ACTION_LABELS[rule.action]}
                    </span>
                    {rule.name}
                  </div>

                  <div className={styles.ruleSummary}>
                    {buildSummary(rule)}
                  </div>
                </div>

                <div className={styles.ruleControls}>
                  <label className={styles.toggle}>
                    <input
                      type="checkbox"
                      checked={rule.active}
                      onChange={() => handleToggle(rule.id)}
                    />
                    <span className={styles.toggleSlider} />
                  </label>

                  <button
                    className={styles.editButton}
                    onClick={() => handleEdit(rule)}
                  >
                    Edit
                  </button>

                  <button
                    className={styles.deleteButton}
                    onClick={() => handleDelete(rule.id)}
                  >
                    Delete
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}

        <RuleEditor
          isOpen={editorOpen}
          rule={editingRule}
          onSave={handleEditorSave}
          onClose={handleEditorClose}
        />
      </PageContentBody>
    </PageContent>
  );
}

export default RulesSettings;
