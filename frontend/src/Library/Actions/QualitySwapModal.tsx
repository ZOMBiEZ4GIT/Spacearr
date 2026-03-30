import React, { useCallback, useState } from 'react';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { LibraryItem, formatSize } from '../LibraryItem';
import styles from './QualitySwapModal.css';

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

interface QualitySwapModalProps {
  isOpen: boolean;
  item: LibraryItem | null;
  onConfirm: (targetQuality: string) => void;
  onCancel: () => void;
}

function QualitySwapModal({
  isOpen,
  item,
  onConfirm,
  onCancel,
}: QualitySwapModalProps) {
  const [targetQuality, setTargetQuality] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleConfirm = useCallback(() => {
    if (!targetQuality) {
      return;
    }

    setIsSubmitting(true);

    // POST /api/v3/action/qualityswap
    fetch('/api/v3/action/qualityswap', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        id: item?.id,
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
  }, [item, targetQuality, onConfirm]);

  if (!item) {
    return null;
  }

  const availableProfiles = QUALITY_PROFILES.filter(
    (q) => q !== item.quality
  );

  const hasSavingsEstimate =
    item.estimatedSavings != null && item.estimatedSavings > 0;

  return (
    <Modal isOpen={isOpen} size="small" onModalClose={onCancel}>
      <ModalContent onModalClose={onCancel}>
        <ModalHeader>Quality Swap</ModalHeader>

        <ModalBody>
          <div className={styles.itemInfo}>
            <div className={styles.itemTitle}>{item.title}</div>

            <div className={styles.currentQuality}>
              Current Quality:{' '}
              <span className={styles.currentQualityValue}>
                {item.quality}
              </span>
            </div>
          </div>

          <div className={styles.fieldGroup}>
            <label className={styles.fieldLabel} htmlFor="targetQuality">
              Target Quality Profile
            </label>

            <select
              id="targetQuality"
              className={styles.qualitySelect}
              value={targetQuality}
              onChange={(e) => setTargetQuality(e.target.value)}
            >
              <option value="">Select a quality profile...</option>

              {availableProfiles.map((profile) => (
                <option key={profile} value={profile}>
                  {profile}
                </option>
              ))}
            </select>
          </div>

          {hasSavingsEstimate && targetQuality && (
            <div className={styles.estimatedChange}>
              Estimated savings:{' '}
              <span className={styles.savingsAmount}>
                {formatSize(item.estimatedSavings!)}
              </span>
              <br />
              {formatSize(item.size)}
              <span className={styles.arrow}>&rarr;</span>
              {formatSize(item.size - item.estimatedSavings!)}
            </div>
          )}
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
            Swap Quality
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

export default QualitySwapModal;
