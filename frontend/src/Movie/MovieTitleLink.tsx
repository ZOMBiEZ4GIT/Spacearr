import React from 'react';
import Link from 'Components/Link/Link';

interface MovieTitleLinkProps {
  titleSlug: string;
  title: string;
  year?: number;
}

function MovieTitleLink({ titleSlug, title, year }: MovieTitleLinkProps) {
  const link = `${window.Spacearr.urlBase}/movie/${titleSlug}`;

  return (
    <Link to={link}>
      {title}
      {year ? ` (${year})` : ''}
    </Link>
  );
}

export default MovieTitleLink;
