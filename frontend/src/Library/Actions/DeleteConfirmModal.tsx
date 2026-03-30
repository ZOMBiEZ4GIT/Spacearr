import React, { useCallback, useState } from 'react';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { LibraryItem, formatSize } from '../LibraryItem';
import styles from './DeleteConfirmModal.css';

interface DeleteConfirmModalProps {
  isOpen: boolean;
  item: LibraryItem | null;
  onConfirm: (unmonitor: boolean) => void;
  onCancel: () => void;
}

function DeleteConfirmModal({
  isOpen,
  item,
  onConfirm,
  onCancel,
}: DeleteConfirmModalProps) {
  const [unmonitor, setUnmonitor] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleConfirm = useCallback(() => {
    setIsSubmitting(true);

    // POST /api/v3/action/delete
    fetch('/api/v3/action/delete', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        id: item?.id,
        unmonitor,
      }),
    })
      .then(() => {
        onConfirm(unmonitor);
      })
      .catch(() => {
        // Error handling would go here
      })
      .finally(() => {
        setIsSubmitting(false);
      });
  }, [item, unmonitor, onConfirm]);

  if (!item) {
    return null;
  }

  const sourceName = item.source === 'radarr' ? 'Radarr' : 'Sonarr';

  return (
    <Modal isOpen={isOpen} size="small" onModalClose={onCancel}>
      <ModalContent onModalClose={onCancel}>
        <ModalHeader>Delete File</ModalHeader>

        <ModalBody>
          <div className={styles.itemInfo}>
            <div className={styles.itemTitle}>{item.title}</div>

            <div className={styles.itemDetail}>
              <span>Path</span>
              <span className={styles.itemDetailValue}>{item.path}</span>
            </div>

            <div className={styles.itemDetail}>
              <span>Quality</span>
              <span className={styles.itemDetailValue}>{item.quality}</span>
            </div>
          </div>

          <div className={styles.sizeFreed}>
            {formatSize(item.size)} will be freed
          </div>

          <div className={styles.warning}>
            This action cannot be undone. The file will be permanently deleted.
          </div>

          <label className={styles.checkboxRow}>
            <input
              type="checkbox"
              checked={unmonitor}
              onChange={(e) => setUnmonitor(e.target.checked)}
            />
            Also unmonitor in {sourceName}
          </label>
        </ModalBody>

        <ModalFooter>
          <Button kind="default" onPress={onCancel}>
            Cancel
          </Button>

          <SpinnerButton
            kind="danger"
            isSpinning={isSubmitting}
            onPress={handleConfirm}
          >
            Delete
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

export default DeleteConfirmModal;
