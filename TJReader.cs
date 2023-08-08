using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Management.Automation;
using System.Diagnostics;

namespace TJLib
{
	public sealed class TJReader
	{
		readonly Lexer lexer = new Lexer();
		long recNo;
		
		public DateTime FileDate { get; set; }
		public string ProcessName { get; set; }
		public int ProcessID { get; set; }
		public Encoding Encoding { get; set; }

        public string ComputerName { get; set; }
		public bool AddEmptyProperties { get; set; }

		public System.Collections.Generic.Dictionary<string, Type> PropertyTypes { get; set; }

		public long Position { get { return lexer.BytesPosition; } }
		public long CharPosition { get { return lexer.CharPosition; } }
		
		public event EventHandler<ErrorEventArgs> ErrorOccured;
		
		static readonly Regex reTime = new Regex("^(?<Minute>\\d{2}):(?<Second>\\d{2}).(?<Fraction>\\d+)-(?<Duration>-?\\d+).*$", RegexOptions.Compiled);
		
		public TJReader()
		{
			this.Encoding = System.Text.Encoding.UTF8;
			this.PropertyTypes = new System.Collections.Generic.Dictionary<string, Type>();
			this.PropertyTypes["OSThread"] = typeof(int);
			this.PropertyTypes["t:clientID"] = typeof(int);
			this.PropertyTypes["t:connectID"] = typeof(int);
			this.PropertyTypes["callWait"] = typeof(int);
			this.PropertyTypes["first"] = typeof(bool);
			this.PropertyTypes["SessionID"] = typeof(int);
			this.PropertyTypes["Method"] = typeof(int);
			this.PropertyTypes["CallID"] = typeof(int);
			this.PropertyTypes["Memory"] = typeof(long);
			this.PropertyTypes["MemoryPeak"] = typeof(long);
			this.PropertyTypes["AvMem"] = typeof(long);
			this.PropertyTypes["InBytes"] = typeof(long);
			this.PropertyTypes["OutBytes"] = typeof(long);
			this.PropertyTypes["CpuTime"] = typeof(long);
			this.PropertyTypes["Rows"] = typeof(long);
			this.PropertyTypes["RowsAffected"] = typeof(long);
			this.PropertyTypes["Trans"] = typeof(bool);
			this.PropertyTypes["dbpid"] = typeof(int);
			this.PropertyTypes["lka"] = typeof(bool);
			this.PropertyTypes["lkp"] = typeof(bool);
		}
		
		public void Open(string filename)
		{
			lexer.Open(filename, this.Encoding);
			recNo = 0;
		}
		
		public void Open(System.IO.Stream stream)
		{
			lexer.Open(stream, this.Encoding);
			recNo = 0;
		}
		
		public void Close()
		{
			lexer.Close();
		}
		
		void OnError(Exception error)
		{
			var handler = ErrorOccured;
			if (handler != null)
				handler(this, new ErrorEventArgs(error));
		}
		
		public PSObject ReadRecord()
		{
			PSObject obj = null;
			ParserState step = ParserState.Begin;
			string propertyName = null;
			long startPos = -1;
			
			do
			{
				long line = lexer.LineNumber;
				int linePos = lexer.LineOffset;
				long filePos = lexer.CharPosition;
				string lexem = lexer.ReadLexem();
				//Debug.WriteLine(String.Format("Parser: {0}, \"{1}\"", step, lexem));
				
				try
				{
					if (lexem == null)
					{
						if (step != ParserState.Begin)
							throw new ParserException("Неожиданный конец файла", step, line, linePos);
						step = ParserState.End;
					}
					
					switch(step)
					{
						case ParserState.Begin:
							{
								obj = new PSObject();
								obj.TypeNames.Insert(0, "TJRecord");
                                obj.Properties.Add(new PSNoteProperty("ComputerName", ComputerName));
								obj.Properties.Add(new PSNoteProperty("ProcessName", ProcessName));
								obj.Properties.Add(new PSNoteProperty("ProcessID", ProcessID));
								obj.Properties.Add(new PSNoteProperty("Line", line));
								obj.Properties.Add(new PSNoteProperty("RecNo", ++recNo));
								startPos = filePos;
								
								// Это начало, сейчас будет время и длительность
								var match = reTime.Match(lexem);
								if (!match.Success)
									throw new ParserException("Неверный формат строки. Ожидается строка в формате \"MM:SS.FFFFFF-D\"", step, line, linePos);
								long minute = Int64.Parse(match.Groups["Minute"].Value);
								long second = Int64.Parse(match.Groups["Second"].Value);
								string fractionStr = match.Groups["Fraction"].Value;
								if (fractionStr.Length < 7)
									fractionStr = (fractionStr + "0000000").Substring(0, 7);
								long fraction = Int64.Parse(fractionStr);
								long timeTicks = minute * TimeSpan.TicksPerMinute + second * TimeSpan.TicksPerSecond + fraction;
								DateTime _date = FileDate.AddTicks(timeTicks);
								obj.Properties.Add(new PSNoteProperty("Date", _date));
								
								long duration = Int64.Parse(match.Groups["Duration"].Value);
								if (duration < 0)
									// Переполнение Int32, такое бывает
									duration += (long)UInt32.MaxValue + 1;
								TimeSpan _duration = new TimeSpan(duration * 1000);
								obj.Properties.Add(new PSNoteProperty("Duration", _duration));
								obj.Properties.Add(new PSNoteProperty("DateStart", _date - _duration));
								step++;
								break;
							}
						case ParserState.EventType:
							{
								obj.Properties.Add(new PSNoteProperty("EventType", lexem));
								step++;
								break;
							}
						case ParserState.Level:
							{
								obj.Properties.Add(new PSNoteProperty("Level", Int32.Parse(lexem)));
								step++;
								break;
							}
						case ParserState.PropertyName:
							{
								propertyName = lexem;
								step++;
								break;
							}
						case ParserState.PropertyValue:
							{
								//Debug.Assert(!String.IsNullOrWhiteSpace(propertyName), "Пустое имя свойства");
								
								string propertyValue;
								if (lexem == ",")
								{
									propertyValue = null;
									step = ParserState.PropertyName;
								}
								else if (lexem == Environment.NewLine)
								{
									propertyValue = null;
									step = ParserState.End;
								}
								else
								{
									propertyValue = lexem;
									step++;
								}
								
								if (AddEmptyProperties || !String.IsNullOrEmpty(propertyValue))
								{
									SetProperty(obj, propertyName, propertyValue);
								}
								
								break;
							}
						case ParserState.BefereEventType:
						case ParserState.BeforeLevel:
						case ParserState.BeforeProperty:
							{
								if (lexem == ",")
									step++;
								else
									throw new ParserException("Ожидается \",\"", step, line, linePos);
								break;
							}
						case ParserState.EqualSign:
							{
								if (lexem == "=")
									step++;
								else
									throw new ParserException("Ожидается \"=\"", step, line, linePos);
								break;
							}
						case ParserState.AfterProperty:
							{
								if (lexem == ",")
									step = ParserState.PropertyName;
								else if (lexem == Environment.NewLine || lexem == null)
								{
									step = ParserState.End;
									//Debug.Assert(startPos >= 0, "startPos не установлен");
									int recordLength = (int)(filePos - startPos);
									obj.Properties.Add(new PSNoteProperty("Length", recordLength));
								}
								else
									throw new ParserException("Ожидается \",\" или конец строки", step, line, linePos);
								break;
							}
					} // switch
				} // try
				catch (Exception ex)
				{
					ParserException error = null;
					if (ex is ParserException)
						error = (ParserException)ex;
					else if (ex is FormatException || ex is OverflowException)
						error = new ParserException(ex.Message, step, line, linePos, ex);
					else
						// Какая-то неизвестная ошибка
						throw;
					OnError(error);
					if (step < ParserState.PropertyName)
						step = ParserState.Error;
					else
						step = ParserState.Unknown;
				}
				
				if (step < 0)
				{
					if (lexem == null || lexem == Environment.NewLine)
						step = ParserState.End;
					else if (step == ParserState.Unknown && lexem == ",")
						step = ParserState.PropertyName;
				}
			}
			while (step != ParserState.End);
			return obj;
		}

		private void SetProperty(PSObject obj, string name, object value)
		{
			if (name == "durationus") // Длительность в микросекундах
			{
				long ms = Int64.Parse((string)value);
				TimeSpan _duration = new TimeSpan(ms * 10);
				obj.Properties["Duration"].Value = _duration;
				obj.Properties["DateStart"].Value = (DateTime)obj.Properties["Date"].Value - _duration;
				return;
			}

			Type valueType;
			if (PropertyTypes.TryGetValue(name, out valueType))
			{
				if (valueType == typeof(int)) {
					value = Int32.Parse((string)value);
				}
				else if (valueType == typeof(long)) {
					value = Int64.Parse((string)value);
				}
				else if (valueType == typeof(bool)) {
					if ((value as string) == "1") {
						value = true;
					}
					else if ((value as string) == "0") {
						value = false;
					}
					else {
						value = Boolean.Parse((string)value);
					}
				}
				else {
					throw new ApplicationException("Unexpected property type");
				}
			}

			var property = obj.Properties[name];
			if (property != null)
			{
				// Такое свойство уже есть. Сделаем в нем коллекцию
				var currentCollection = property.Value as object[];
				int valueCount = currentCollection != null ? currentCollection.Length : 1;
				object[] collection = new object[valueCount + 1];
				if (currentCollection != null)
					currentCollection.CopyTo(collection, 0);
				else {
					collection[0] = property.Value;
					property.Value = collection;
				}
				collection[valueCount] = value;
			}
			else
				obj.Properties.Add(new PSNoteProperty(name, value));
		}
	}
}