import React, { useCallback } from 'react';
import { LibraryItem, formatSize } from './LibraryItem';
import BitrateBadge from './BitrateBadge';
import SourceBadge from './SourceBadge';
import styles from './LibraryTableRow.css';

interface LibraryTableRowProps {
  item: LibraryItem;
  isSelected: boolean;
  onSelect: (id: number, selected: boolean) => void;
  onClick: (item: LibraryItem) => void;
}

function LibraryTableRow({
  item,
  isSelected,
  onSelect,
  onClick,
}: LibraryTableRowProps) {
  const handleCheckboxChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      event.stopPropagation();
      onSelect(item.id, event.target.checked);
    },
    [item.id, onSelect]
  );

  const handleRowClick = useCallback(() => {
    onClick(item);
  }, [item, onClick]);

  const handleCheckboxClick = useCallback(
    (event: React.MouseEvent) => {
      event.stopPropagation();
    },
    []
  );

  const subtitle = [item.quality, item.codec, item.resolution]
    .filter(Boolean)
    .join(' \u2022 ');

  return (
    <tr className={styles.row} onClick={handleRowClick}>
      <td className={styles.checkboxCell} onClick={handleCheckboxClick}>
        <input
          type="checkbox"
          checked={isSelected}
          onChange={handleCheckboxChange}
        />
      </td>

      <td className={styles.titleCell}>
        <div className={styles.title}>{item.title}</div>
        <div className={styles.subtitle}>{subtitle}</div>
      </td>

      <td className={styles.sizeCell}>
        {formatSize(item.size)}
      </td>

      <td className={styles.bitrateCell}>
        <BitrateBadge bitrate={item.bitrate} />
      </td>

      <td className={styles.qualityCell}>
        {item.quality}
      </td>

      <td className={styles.sourceCell}>
        <SourceBadge source={item.source} />
      </td>
    </tr>
  );
}

export default LibraryTableRow;
