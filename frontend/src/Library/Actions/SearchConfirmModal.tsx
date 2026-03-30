import React, { useCallback, useState } from 'react';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { LibraryItem } from '../LibraryItem';
import styles from './SearchConfirmModal.css';

interface SearchConfirmModalProps {
  isOpen: boolean;
  item: LibraryItem | null;
  onConfirm: () => void;
  onCancel: () => void;
}

function SearchConfirmModal({
  isOpen,
  item,
  onConfirm,
  onCancel,
}: SearchConfirmModalProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleConfirm = useCallback(() => {
    setIsSubmitting(true);

    // POST /api/v3/action/search
    fetch('/api/v3/action/search', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        id: item?.id,
      }),
    })
      .then(() => {
        onConfirm();
      })
      .catch(() => {
        // Error handling would go here
      })
      .finally(() => {
        setIsSubmitting(false);
      });
  }, [item, onConfirm]);

  if (!item) {
    return null;
  }

  const sourceName = item.source === 'radarr' ? 'Radarr' : 'Sonarr';

  return (
    <Modal isOpen={isOpen} size="small" onModalClose={onCancel}>
      <ModalContent onModalClose={onCancel}>
        <ModalHeader>Search & Replace</ModalHeader>

        <ModalBody>
          <div className={styles.itemInfo}>
            <div className={styles.itemTitle}>{item.title}</div>

            {item.qualityProfile && (
              <span className={styles.profileBadge}>
                {item.qualityProfile}
              </span>
            )}
          </div>

          <div className={styles.description}>
            This will trigger a new search in{' '}
            <span className={styles.sourceHighlight}>{sourceName}</span> for a
            release matching the current quality profile. The existing file will
            be replaced when a suitable release is found.
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
            Search
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

export default SearchConfirmModal;
