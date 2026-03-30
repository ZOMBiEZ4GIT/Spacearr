import React, { useCallback, useState } from 'react';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { LibraryItem, formatSize } from '../LibraryItem';
import styles from './BulkDowngradeModal.css';

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

interface BulkDowngradeModalProps {
  isOpen: boolean;
  items: LibraryItem[];
  onConfirm: (targetQuality: string) => void;
  onCancel: () => void;
}

function BulkDowngradeModal({
  isOpen,
  items,
  onConfirm,
  onCancel,
}: BulkDowngradeModalProps) {
  const [targetQuality, setTargetQuality] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const totalSize = items.reduce((sum, item) => sum + item.size, 0);

  const handleConfirm = useCallback(() => {
    if (!targetQuality) {
      return;
    }

    setIsSubmitting(true);

    const ids = items.map((item) => item.id);

    fetch('/api/v3/action/qualityswap', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        ids,
        targetQuality,
      }),
    })
      .then(() => {
        onConfirm(targetQuality);
      })
      .catch(() => {
        // Error handling would go here
      })
      .finally(() => {
        setIsSubmitting(false);
      });
  }, [items, targetQuality, onConfirm]);

  return (
    <Modal isOpen={isOpen} size="small" onModalClose={onCancel}>
      <ModalContent onModalClose={onCancel}>
        <ModalHeader>Bulk Downgrade</ModalHeader>

        <ModalBody>
          <div className={styles.summary}>
            <div className={styles.summaryCount}>
              {items.length} item{items.length !== 1 ? 's' : ''} selected
            </div>

            <div className={styles.summaryDetail}>
              Total size: {formatSize(totalSize)}
            </div>
          </div>

          <div className={styles.fieldGroup}>
            <label className={styles.fieldLabel} htmlFor="bulkTargetQuality">
              Target Quality Profile
            </label>

            <select
              id="bulkTargetQuality"
              className={styles.qualitySelect}
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

          <div className={styles.description}>
            All selected items will be searched for a new release matching the
            target quality profile. Existing files will be replaced when
            suitable releases are found.
          </div>
        </ModalBody>

        <ModalFooter>
          <Button kind="default" onPress={onCancel}>
            Cancel
          </Button>

          <SpinnerButton
            kind="primary"
            isSpinning={isSubmitting}
            onPress={handleConfirm}
          >
            Downgrade {items.length} Item{items.length !== 1 ? 's' : ''}
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

export default BulkDowngradeModal;
