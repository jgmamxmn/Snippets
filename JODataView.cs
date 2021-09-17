using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace MxmnAI
{
	public class JODataView : IDataView
	{
		internal List<JObject> JObjects;
		internal string IndicativeColumn;
		internal Dictionary<string, Func<string, MLContext, IDataView, IDataView>> Transformers;
		internal List<string> FeaturableColumns;

		public JODataView(IEnumerable<JObject> paramJObjects, string paramIndicativeColumn) : base()
		{
			JObjects = new List<JObject>(paramJObjects);
			IndicativeColumn = paramIndicativeColumn;

			FeaturableColumns = new List<string>();

			Transformers = new Dictionary<string, Func<string, MLContext, IDataView, IDataView>>();

			var builder = new DataViewSchema.Builder();

			void Subprocess(JObject aJO, string Pfx, string ctr_pfx, int ctr)
			{
				try
				{
					foreach (var prop in aJO.Properties())
					{
						++ctr;

						string N = prop.Name;
						DataViewType DVT;
						Func<string, MLContext, IDataView, IDataView> Transformer = null;

						switch (prop.Value.Type)
						{
							case JTokenType.Boolean:
								DVT = BooleanDataViewType.Instance;
								Transformer = (n, c, v) => v;
								break;
							case JTokenType.Float:
								DVT = NumberDataViewType.Double;
								Transformer = (n, c, v) =>
								{
									var MinMax = c.Transforms.NormalizeMinMax(n);
									return MinMax.Fit(v).Transform(v);
								};
								break;
							case JTokenType.Null:
								prop.Value = 0;
								goto case JTokenType.Integer;
							case JTokenType.Integer:
								prop.Value = (float)prop.Value;
								goto case JTokenType.Float;
							case JTokenType.String:
								DVT = TextDataViewType.Instance;
								Transformer = (n, c, v) =>
								{
									return v;
								};
								break;
							case JTokenType.Object:
								Subprocess((JObject)prop.Value, Pfx + prop.Name + ".", ctr_pfx + "." + ctr.ToString(), 0);
								DVT = null;
								Transformer = null;
								break;
							default:
								throw new Exception("Unsupported type '" + prop.Value.Type.ToString() + "' (" + prop.Value.Type + ") of field '" + Pfx + prop.Name + "'.");
						}

						if (DVT is object)
						{
							if (N == IndicativeColumn)
							{
								builder.AddColumn(Pfx + N, DVT);
								Transformers.Add(Pfx + N, Transformer);
							}
							else
							{
								builder.AddColumn(Pfx + N, DVT);
								Transformers.Add(Pfx + N, Transformer);
								if (DVT is TextDataViewType)
								{
									FeaturableColumns.Add(Pfx + N);
								}
								else
								{
									string TxCol = "__Tx__" + Pfx + N;
									FeaturableColumns.Add(TxCol);
									builder.AddColumn(TxCol, TextDataViewType.Instance);
									Transformers.Add(TxCol, (n, c, v) => v);
								}
							}
						}
					}
				}
				#pragma warning disable S2737 // "catch" clauses should do more than rethrow - but I want to keep this for debugging later
				catch (Exception)
				{
					throw;
				}
				#pragma warning restore S2737 // "catch" clauses should do more than rethrow
			}
			Subprocess(paramJObjects.First(), "", "", 0); // This is just to build the schema, so take the first item as the canonical example of what columns each row should contain

			builder.AddColumn("Features", NumberDataViewType.Single);
			Schema = builder.ToSchema();
		}

		public bool CanShuffle => false;

		public DataViewSchema Schema { get; }

		public long? GetRowCount()
		{
			return JObjects.Count;
		}

		public DataViewRowCursor GetRowCursor(
				IEnumerable<DataViewSchema.Column> columnsNeeded,
				Random rand = null)

		{
			return new JOCursor(this);
		}

		public DataViewRowCursor[] GetRowCursorSet(
			IEnumerable<DataViewSchema.Column> columnsNeeded, int n,
			Random rand = null)
		{
			return new[] { GetRowCursor(columnsNeeded, rand) };
		}

		class JOCursor : DataViewRowCursor
		{
			JODataView Papa;
			int _position;
			public JOCursor(JODataView paramPapa) : base()
			{
				Papa = paramPapa;
				_position = -1;
				Schema = Papa.Schema;
			}
			public override long Position { get => _position; }

			public override long Batch => 0;

			public override DataViewSchema Schema { get; }

			private class MyGetter<TValue>
			{
				Func<TValue> MyDelegate;
				public MyGetter(Func<TValue> myDelegate)
				{
					MyDelegate = myDelegate;
				}
				internal void DoGet(ref TValue X)
				{
					X = MyDelegate();
				}
			}
			Dictionary<string, object> Getters = new Dictionary<string, object>();
			public override ValueGetter<TValue> GetGetter<TValue>(DataViewSchema.Column column)
			{
				JToken SeekToJO(JObject ParentObj, string QualifiedPath)
				{
					string[] SA = QualifiedPath.Split(new[] { '.' });
					JToken JT = ParentObj[SA[0]];
					for (int i = 1; i < SA.Length; ++i)
						JT = JT?[SA[i]];
					return JT;
				}

				if (!Getters.ContainsKey(column.Name))
				{
					MyGetter<TValue> X;
					if (column.Name.Length > 6 && column.Name.Substring(0, 6) == "__Tx__")
					{
						X = new MyGetter<TValue>(() =>
						{
							var JT = SeekToJO(Papa.JObjects[_position], column.Name.Substring(6));
							if (JT is null) return default(TValue);
							return (TValue)(object)JT.Value<string>().AsMemory();
						});
					}
					else if (column.Name.Length > 7 && column.Name.Substring(0, 7) == "__Lbl__")
					{
						X = new MyGetter<TValue>(() =>
						{
							var JT = SeekToJO(Papa.JObjects[_position], column.Name.Substring(7));
							if (JT is null) return default(TValue);
							return (TValue)(object)(JT.Value<bool>());
						});
					}
					else
					{
						X = new MyGetter<TValue>(() =>
						{
							var JT = SeekToJO(Papa.JObjects[_position], column.Name);

							if (JT is null)
							{
								return default(TValue);
							}

							switch (JT.Type)
							{
								case JTokenType.Null:
									return default(TValue);
								case JTokenType.String:
									return (TValue)(object)JT.Value<string>().AsMemory();
								default:
									return JT.Value<TValue>();
							}
						});
					}
					Getters.Add(column.Name, X);
				}
				return ((MyGetter<TValue>)Getters[column.Name]).DoGet;
			}

			private void MyIdGetter(ref DataViewRowId outp)
			{
				outp = new DataViewRowId((ulong)_position, 0);
			}

			public override ValueGetter<DataViewRowId> GetIdGetter()
			{
				return MyIdGetter;
			}

			public override bool IsColumnActive(DataViewSchema.Column column)
			{
				return true;
			}
			public override bool MoveNext()
			{
				++_position;
				return (Position < Papa.GetRowCount());
			}
		}
	}
}
