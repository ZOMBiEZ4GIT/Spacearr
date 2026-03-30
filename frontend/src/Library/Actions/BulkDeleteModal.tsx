import React, { useCallback, useState } from 'react';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { LibraryItem, formatSize } from '../LibraryItem';
import styles from './BulkDeleteModal.css';

interface BulkDeleteModalProps {
  isOpen: boolean;
  items: LibraryItem[];
  onConfirm: (unmonitor: boolean) => void;
  onCancel: () => void;
}

function BulkDeleteModal({
  isOpen,
  items,
  onConfirm,
  onCancel,
}: BulkDeleteModalProps) {
  const [unmonitor, setUnmonitor] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const totalSize = items.reduce((sum, item) => sum + item.size, 0);

  const handleConfirm = useCallback(() => {
    setIsSubmitting(true);

    const ids = items.map((item) => item.id);

    fetch('/api/v3/action/delete', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        ids,
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
  }, [items, unmonitor, onConfirm]);

  return (
    <Modal isOpen={isOpen} size="small" onModalClose={onCancel}>
      <ModalContent onModalClose={onCancel}>
        <ModalHeader>Bulk Delete</ModalHeader>

        <ModalBody>
          <div className={styles.summary}>
            <div className={styles.summaryCount}>
              {items.length} item{items.length !== 1 ? 's' : ''} selected
            </div>

            <div className={styles.summarySize}>
              Total size: {formatSize(totalSize)}
            </div>
          </div>

          <div className={styles.sizeFreed}>
            {formatSize(totalSize)} will be freed
          </div>

          <div className={styles.warning}>
            This action cannot be undone. All selected files will be permanently
            deleted.
          </div>

          <label className={styles.checkboxRow}>
            <input
              type="checkbox"
              checked={unmonitor}
              onChange={(e) => setUnmonitor(e.target.checked)}
            />
            Also unmonitor in Radarr/Sonarr
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
            Delete {items.length} File{items.length !== 1 ? 's' : ''}
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

export default BulkDeleteModal;
