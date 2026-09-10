import { categoryColor, heatColor } from '../lib/heat';
import s from './Treemap.module.css';

export default function Legend({ colorBy, heatMode, categories = [] }: { colorBy: string; heatMode: string; categories?: string[] }) {
  if (colorBy === 'heat') {
    const gradient = `linear-gradient(90deg, ${[0, 0.25, 0.5, 0.75, 1].map((t) => heatColor(t)).join(', ')})`;
    return <div className={s.legend}><span>Efficient</span><span className={s.bar} style={{ background: gradient }} /><span>Bloated</span><span>· {heatMode === 'absolute' ? 'bits per pixel vs fixed thresholds' : 'ranked within this view'}</span></div>;
  }
  if (colorBy === 'duplicates') return <div className={s.legend}><span><i className={s.swatch} style={{ background: '#D14D4D' }} />Has a duplicate</span><span><i className={s.swatch} style={{ background: '#4B5563' }} />Unique</span></div>;
  return <div className={s.legend}>{categories.slice(0, 12).map((c) => <span key={c}><i className={s.swatch} style={{ background: categoryColor(c) }} />{c}</span>)}</div>;
}
