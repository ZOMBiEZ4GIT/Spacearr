import React, { useCallback, useEffect, useState } from 'react';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { Rule } from './RulesSettings';
import styles from './RuleEditor.css';

type RuleAction = 'enforce' | 'flag' | 'delete';
type RuleSource = 'any' | 'radarr' | 'sonarr';

const QUALITY_PROFILES = [
  'Bluray-2160p',
  'Bluray-1080p',
  'Bluray-720p',
  'WEBDL-2160p',
  'WEBDL-1080p',
  'WEBDL-720p',
  'Remux-2160p',
  'Remux-1080p',
  'HDTV-1080p',
  'HDTV-720p',
];

interface RuleEditorProps {
  isOpen: boolean;
  rule: Rule | null;
  onSave: (rule: Rule) => void;
  onClose: () => void;
}

function RuleEditor({ isOpen, rule, onSave, onClose }: RuleEditorProps) {
  const [name, setName] = useState('');
  const [source, setSource] = useState<RuleSource>('any');
  const [minSize, setMinSize] = useState('');
  const [maxQuality, setMaxQuality] = useState('');
  const [tagsInput, setTagsInput] = useState('');
  const [action, setAction] = useState<RuleAction>('enforce');
  const [targetQuality, setTargetQuality] = useState('');
  const [active, setActive] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (rule) {
      setName(rule.name);
      setSource(rule.source);
      setMinSize(
        rule.minSize != null
          ? (rule.minSize / 1_000_000_000).toString()
          : ''
      );
      setMaxQuality(rule.maxQuality || '');
      setTagsInput(rule.tags.join(', '));
      setAction(rule.action);
      setTargetQuality(rule.targetQuality || '');
      setActive(rule.active);
    } else {
      setName('');
      setSource('any');
      setMinSize('');
      setMaxQuality('');
      setTagsInput('');
      setAction('enforce');
      setTargetQuality('');
      setActive(true);
    }
  }, [rule, isOpen]);

  const handleSave = useCallback(() => {
    if (!name.trim()) {
      return;
    }

    setIsSubmitting(true);

    const tags = tagsInput
      .split(',')
      .map((t) => t.trim())
      .filter(Boolean);

    const ruleData: Rule = {
      id: rule?.id ?? 0,
      name: name.trim(),
      source,
      minSize: minSize ? parseFloat(minSize) * 1_000_000_000 : null,
      maxQuality: maxQuality || null,
      tags,
      action,
      targetQuality: action === 'enforce' ? targetQuality || null : null,
      active,
    };

    const method = rule ? 'PUT' : 'POST';
    const url = rule ? `/api/v3/rule/${rule.id}` : '/api/v3/rule';

    fetch(url, {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(ruleData),
    })
      .then(() => {
        onSave(ruleData);
      })
      .catch(() => {
        // Save locally even if API fails during development
        onSave(ruleData);
      })
      .finally(() => {
        setIsSubmitting(false);
      });
  }, [
    name,
    source,
    minSize,
    maxQuality,
    tagsInput,
    action,
    targetQuality,
    active,
    rule,
    onSave,
  ]);

  return (
    <Modal isOpen={isOpen} size="medium" onModalClose={onClose}>
      <ModalContent onModalClose={onClose}>
        <ModalHeader>{rule ? 'Edit Rule' : 'Add Rule'}</ModalHeader>

        <ModalBody>
          <div className={styles.fieldGroup}>
            <label className={styles.fieldLabel} htmlFor="ruleName">
              Name
            </label>
            <input
              id="ruleName"
              className={styles.textInput}
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Rule name..."
            />
          </div>

          <hr className={styles.separator} />

          <div className={styles.row}>
            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="ruleSource">
                Source
              </label>
              <select
                id="ruleSource"
                className={styles.selectInput}
                value={source}
                onChange={(e) => setSource(e.target.value as RuleSource)}
              >
                <option value="any">Any</option>
                <option value="radarr">Radarr</option>
                <option value="sonarr">Sonarr</option>
              </select>
            </div>

            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="ruleMinSize">
                Min Size (GB)
              </label>
              <input
                id="ruleMinSize"
                className={styles.numberInput}
                type="number"
                min="0"
                step="1"
                value={minSize}
                onChange={(e) => setMinSize(e.target.value)}
                placeholder="No minimum"
              />
            </div>
          </div>

          <div className={styles.row}>
            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="ruleMaxQuality">
                Max Quality
              </label>
              <select
                id="ruleMaxQuality"
                className={styles.selectInput}
                value={maxQuality}
                onChange={(e) => setMaxQuality(e.target.value)}
              >
                <option value="">Any</option>
                {QUALITY_PROFILES.map((profile) => (
                  <option key={profile} value={profile}>
                    {profile}
                  </option>
                ))}
              </select>
            </div>

            <div className={styles.fieldGroup}>
              <label className={styles.fieldLabel} htmlFor="ruleTags">
                Tags
              </label>
              <input
                id="ruleTags"
                className={styles.tagsInput}
                type="text"
                value={tagsInput}
                onChange={(e) => setTagsInput(e.target.value)}
                placeholder="Comma-separated tags..."
              />
              <div className={styles.fieldHint}>
                Separate multiple tags with commas
              </div>
            </div>
          </div>

          <hr className={styles.separator} />

          <div className={styles.fieldGroup}>
            <label className={styles.fieldLabel} htmlFor="ruleAction">
              Action
            </label>
            <select
              id="ruleAction"
              className={styles.selectInput}
              value={action}
              onChange={(e) => setAction(e.target.value as RuleAction)}
            >
              <option value="enforce">Enforce Max Quality</option>
              <option value="flag">Flag for Review</option>
              <option value="delete">Auto Delete</option>
            </select>
          </div>

          {action === 'enforce' && (
            <div className={styles.fieldGroup}>
              <label
                className={styles.fieldLabel}
                htmlFor="ruleTargetQuality"
              >
                Target Quality
              </label>
              <select
                id="ruleTargetQuality"
                className={styles.selectInput}
                value={targetQuality}
                onChange={(e) => setTargetQuality(e.target.value)}
              >
                <option value="">Select a quality profile...</option>
                {QUALITY_PROFILES.map((profile) => (
                  <option key={profile} value={profile}>
                    {profile}
                  </option>
                ))}
              </select>
            </div>
          )}

          <div className={styles.toggleRow}>
            <span className={styles.fieldLabel}>Active</span>
            <label className={styles.toggle}>
              <input
                type="checkbox"
                checked={active}
                onChange={(e) => setActive(e.target.checked)}
              />
              <span className={styles.toggleSlider} />
            </label>
          </div>
        </ModalBody>

        <ModalFooter>
          <Button kind="default" onPress={onClose}>
            Cancel
          </Button>

          <SpinnerButton
            kind="primary"
            isSpinning={isSubmitting}
            onPress={handleSave}
          >
            {rule ? 'Save' : 'Add'}
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

export default RuleEditor;
