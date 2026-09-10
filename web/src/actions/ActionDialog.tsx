import type { ProfileEstimate } from '../api/types';

export interface ActionDialogProps {
  kind: 'delete' | 'replace';
  itemId: number;
  profile?: ProfileEstimate;
  onClose: () => void;
  onDone: () => void;
}

/** Placeholder until Task 7 builds the real preview/confirm dialog. */
export default function ActionDialog(_props: ActionDialogProps) {
  return null;
}
