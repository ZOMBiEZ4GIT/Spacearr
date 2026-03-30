import React, { useCallback, useMemo, useState } from 'react';
import { SortDirection } from 'Helpers/Props/sortDirections';
import { LibraryItem, MOCK_LIBRARY_ITEMS } from './LibraryItem';
import LibraryTableRow from './LibraryTableRow';
import styles from './LibraryTable.css';

interface LibraryTableProps {
  items?: LibraryItem[];
  sortKey: string;
  sortDirection: SortDirection;
  selectedIds: number[];
  sourceFilter: string;
  minSizeFilter: number;
  onSortChange: (sortKey: string) => void;
  onSelectionChange: (selectedIds: number[]) => void;
  onItemClick: (item: LibraryItem) => void;
}

function getSortValue(item: LibraryItem, key: string): string | number {
  switch (key) {
    case 'title':
      return item.title.toLowerCase();
    case 'size':
      return item.size;
    case 'bitrate':
      return item.bitrate;
    case 'quality':
      return item.quality;
    case 'source':
      return item.source;
    default:
      return 0;
  }
}

function LibraryTable({
  items,
  sortKey,
  sortDirection,
  selectedIds,
  sourceFilter,
  minSizeFilter,
  onSortChange,
  onSelectionChange,
  onItemClick,
}: LibraryTableProps) {
  const displayItems = items && items.length > 0 ? items : MOCK_LIBRARY_ITEMS;

  const filteredAndSorted = useMemo(() => {
    let filtered = displayItems;

    if (sourceFilter && sourceFilter !== 'all') {
      filtered = filtered.filter((item) => item.source === sourceFilter);
    }

    if (minSizeFilter > 0) {
      const minBytes = minSizeFilter * 1024 * 1024 * 1024;
      filtered = filtered.filter((item) => item.size >= minBytes);
    }

    const sorted = [...filtered].sort((a, b) => {
      const aVal = getSortValue(a, sortKey);
      const bVal = getSortValue(b, sortKey);

      if (typeof aVal === 'string' && typeof bVal === 'string') {
        return sortDirection === 'ascending'
          ? aVal.localeCompare(bVal)
          : bVal.localeCompare(aVal);
      }

      if (typeof aVal === 'number' && typeof bVal === 'number') {
        return sortDirection === 'ascending' ? aVal - bVal : bVal - aVal;
      }

      return 0;
    });

    return sorted;
  }, [displayItems, sortKey, sortDirection, sourceFilter, minSizeFilter]);

  const allSelected =
    filteredAndSorted.length > 0 &&
    filteredAndSorted.every((item) => selectedIds.includes(item.id));

  const handleSelectAll = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      if (event.target.checked) {
        onSelectionChange(filteredAndSorted.map((item) => item.id));
      } else {
        onSelectionChange([]);
      }
    },
    [filteredAndSorted, onSelectionChange]
  );

  const handleSelect = useCallback(
    (id: number, selected: boolean) => {
      if (selected) {
        onSelectionChange([...selectedIds, id]);
      } else {
        onSelectionChange(selectedIds.filter((sid) => sid !== id));
      }
    },
    [selectedIds, onSelectionChange]
  );

  const handleSort = useCallback(
    (key: string) => {
      onSortChange(key);
    },
    [onSortChange]
  );

  const sortIndicator = (key: string) => {
    if (sortKey !== key) {
      return null;
    }

    return sortDirection === 'ascending' ? ' \u25B2' : ' \u25BC';
  };

  return (
    <table className={styles.table}>
      <thead>
        <tr>
          <th style={{ width: 36, textAlign: 'center' }}>
            <input
              type="checkbox"
              checked={allSelected}
              onChange={handleSelectAll}
            />
          </th>
          <th
            style={{ cursor: 'pointer' }}
            onClick={() => handleSort('title')}
          >
            Title{sortIndicator('title')}
          </th>
          <th
            style={{ cursor: 'pointer', textAlign: 'right' }}
            onClick={() => handleSort('size')}
          >
            Size{sortIndicator('size')}
          </th>
          <th
            style={{ cursor: 'pointer', textAlign: 'center' }}
            onClick={() => handleSort('bitrate')}
          >
            Bitrate{sortIndicator('bitrate')}
          </th>
          <th
            style={{ cursor: 'pointer' }}
            onClick={() => handleSort('quality')}
          >
            Quality{sortIndicator('quality')}
          </th>
          <th
            style={{ cursor: 'pointer', textAlign: 'center' }}
            onClick={() => handleSort('source')}
          >
            Source{sortIndicator('source')}
          </th>
        </tr>
      </thead>
      <tbody>
        {filteredAndSorted.map((item) => (
          <LibraryTableRow
            key={item.id}
            item={item}
            isSelected={selectedIds.includes(item.id)}
            onSelect={handleSelect}
            onClick={onItemClick}
          />
        ))}

        {filteredAndSorted.length === 0 && (
          <tr>
            <td colSpan={6} style={{ textAlign: 'center', padding: 40 }}>
              No items match current filters
            </td>
          </tr>
        )}
      </tbody>
    </table>
  );
}

export default LibraryTable;
