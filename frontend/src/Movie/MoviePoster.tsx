import React from 'react';
import { Image } from './Movie';

interface MoviePosterProps {
  className?: string;
  images: Image[];
  size?: number;
  lazy?: boolean;
  overflow?: boolean;
  style?: React.CSSProperties;
}

function MoviePoster({ className, images, size = 250 }: MoviePosterProps) {
  const posterImage = images.find((i) => i.coverType === 'poster');
  const src = posterImage?.remoteUrl || posterImage?.url;

  return (
    <img
      className={className}
      src={src}
      alt=""
      style={{ width: size, height: 'auto' }}
    />
  );
}

export default MoviePoster;
