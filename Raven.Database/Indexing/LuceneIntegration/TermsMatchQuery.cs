﻿using System;
using System.Collections.Generic;
using Lucene.Net.Index;
using Lucene.Net.Search;
using System.Linq;

namespace Raven.Database.Indexing.LuceneIntegration
{
	public interface IRavenLuceneMethodQuery
	{
		IRavenLuceneMethodQuery Merge(Query other);
	}

	public class TermsMatchQuery : MultiTermQuery, IRavenLuceneMethodQuery
	{
		private readonly string field;
		private List<string> matches;
		public List<string> Matches
		{
			get { return matches; }
		}
		public string Field
		{
			get { return field; }
		}

		public TermsMatchQuery(string field, IEnumerable<string> matches)
		{
			this.field = field;
			this.matches = matches.OrderBy(s => s, StringComparer.Ordinal).ToList();
		}

		public override string ToString(string fld)
		{
			return "@in(" + field + ", " + string.Join(", ", matches) + ")";
		}

		protected override FilteredTermEnum GetEnum(IndexReader reader)
		{
			return new RavenTermsFilteredTermEnum(this, reader);
		}

		private sealed class RavenTermsFilteredTermEnum : FilteredTermEnum
		{
			private readonly TermsMatchQuery termsMatchQuery;
			private readonly IndexReader reader;
			private bool endEnum;
			private int pos;

			public RavenTermsFilteredTermEnum(TermsMatchQuery termsMatchQuery, IndexReader reader)
			{
				this.termsMatchQuery = termsMatchQuery;
				this.reader = reader;
				if (this.termsMatchQuery.matches.Count == 0)
				{
					endEnum = true;
					return;
				}
				MoveToCurrentTerm();
			}

			private void MoveToCurrentTerm()
			{
				SetEnum(reader.Terms(new Term(termsMatchQuery.field, termsMatchQuery.matches[pos])));
			}

			protected override bool TermCompare(Term term)
			{
				if (term.Field != termsMatchQuery.field)
				{
					endEnum = true;
					return false;
				}
				int last;
				while ((last = string.CompareOrdinal(termsMatchQuery.matches[pos], term.Text)) < 0)
				{
					if (++pos >= termsMatchQuery.matches.Count)
					{
						endEnum = true;
						return false;
					}
				}
				if (last > 0)
				{
					MoveToCurrentTerm();
					return currentTerm != null;
				}
				return last == 0;
			}

			public override float Difference()
			{
				return 1.0f;
			}

			public override bool EndEnum()
			{
				return endEnum;
			}
		}

		public IRavenLuceneMethodQuery Merge(Query other)
		{
			var termsMatchQuery = (TermsMatchQuery) other;
			matches.Add(termsMatchQuery.field);
			matches.AddRange(termsMatchQuery.matches);

			matches = matches.Distinct()
				.Where(x=>string.IsNullOrWhiteSpace(x) == false)
				.OrderBy(s => s, StringComparer.Ordinal).ToList();
			return this;
		}
	}
}