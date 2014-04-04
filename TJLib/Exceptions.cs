/*
 * Created by SharpDevelop.
 * User: Kuzin.A
 * Date: 03.04.2014
 * Time: 15:11
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

using System;
using System.Runtime.Serialization;

namespace TJLib
{
	public class ParserException : ApplicationException
	{
		public long LineNumber { get; private set; }
		public int LinePos { get; private set; }
		public ParserState ParserState { get; private set; }
		
		public ParserException(string message = null, ParserState parserState = ParserState.Unknown, long lineNumber = 0, int linePos = 0, Exception innerException = null)
			: base(message, innerException)
		{
			this.ParserState = parserState;
			this.LineNumber = lineNumber;
			this.LinePos = linePos;
		}
		
		public ParserException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.ParserState = (ParserState)info.GetValue("ParserState", typeof(ParserState));
			LineNumber = info.GetInt64("LineNumber");
			LinePos = info.GetInt32("LinePos");
		}
		
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("ParserState", ParserState, typeof(ParserState));
			info.AddValue("LineNumber", LineNumber, typeof(long));
			info.AddValue("LinePos", LinePos, typeof(int));
		}
	}
}