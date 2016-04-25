using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnostics;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using System;

namespace ContainsTokenBenchmark
{
    [Config(typeof(Config))]
    public class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<Program>();
        }

        private class Config : ManualConfig
        {
            public Config()
            {
                Add(Job.Clr.WithLaunchCount(1));
                Add(new GCDiagnoser());
            }
        }

        [Params("Foo;Bar", "Foo;FooBar;Whatever", "Bar;blaat;foo", "blaat;foo;Bar", "foo;Bar;Blaat", "foo;FooBar;Blaat",
            "Bar1;Bar2;Bar3;Bar4;Bar", "Bar1;Bar2;Bar3;Bar4;NoMatch", "Foo;FooBar;Whatever", "Some;Other;Really;Interesting;Tokens")]
        public string Value { get; set; }

        public string Match = "Bar";
        private static char[] delimeter = new[] { ';' };

        [Benchmark(Baseline = true)]
        public bool StringSplit()
        {
            // To make is a fair comparision!
            if (string.IsNullOrEmpty(Match)) return false;
            if (string.IsNullOrEmpty(Value)) return false;

            var tokens = Value.Split(delimeter);
            foreach (var token in tokens)
            {
                if (token == Match)
                    return true;
            }
            return false;
        }

        [Benchmark]
        public bool ContainsToken()
        {
            return ContainsToken(Value, Match);
        }

        [Benchmark]
        public bool ContainsTokenXoofx()
        {
            return ContainsTokenXoofx(Value, Match);
        }

        [Benchmark]
        public bool ContainsTokenXoofxUnsafe()
        {
            return ContainsTokenXoofxUnsafe(Value, Match);
        }

        [Benchmark]
        public bool ContainsTokenHellBrick()
        {
            return ContainsTokenHellBrick(Value, Match);
        }

        [Benchmark]
        public bool ContainsTokenMonty()
        {
            return ContainsTokenMonty(Value, Match);
        }

        [Benchmark]
        public bool ContainsTokenSingleScan()
        {
            return ContainsTokenSingleScan(Value, Match);
        }

        /// <summary>
        /// Code taken from https://twitter.com/Nick_Craver/status/722741298575319040
        /// Checks whether the *entire* token exists, correctly handling seperators
        /// (optional at start and end) so: "Foo;Bar" contains "Bar", but
        /// "Foo;FooBar;Whatever" does not
        /// </summary>        
        public static bool ContainsToken(string value, string token, char delimiter = ';')
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (string.IsNullOrEmpty(value)) return false;

            int lastIndex = -1, idx, endIndex = value.Length - token.Length, tokenLength = token.Length;
            while ((idx = value.IndexOf(token, lastIndex + 1)) > lastIndex)
            {
                lastIndex = idx;
                if ((idx == 0 || (value[idx - 1] == delimiter))
                    && (idx == endIndex || (value[idx + tokenLength] == delimiter)))
                {
                    return true;
                }
            }
            return false;
        }

        // See https://gist.github.com/mattwarren/f0594a9f3afa9377a4bbc2bcf8e573c5#gistcomment-1757392
        public static bool ContainsTokenXoofx(string value, string token, char delimiter = ';')
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (string.IsNullOrEmpty(value)) return false;

            int length = value.Length;
            int tokenLength = token.Length;

            for (int i = 0; i < length; i++)
            {
                for (int j = 0; j < tokenLength; j++, i++)
                {
                    if (i >= length || value[i] != token[j])
                    {
                        goto next;
                    }
                }

                if ((i == length || (i < length && value[i] == delimiter)))
                {
                    return true;
                }

            next:
                i = value.IndexOf(delimiter, i);
                if (i < 0)
                {
                    break;
                }
            }

            return false;
        }

        public unsafe static bool ContainsTokenXoofxUnsafe(string value, string token, char delimiter = ';')
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (string.IsNullOrEmpty(value)) return false;

            int length = value.Length;
            int tokenLength = token.Length;
            int i = 0;
            fixed (char* pValue = value)
            fixed (char* pToken = token)
            while (true)
            {
                for (int j = 0; j < tokenLength; j++, i++)
                {
                    if (i >= length || pValue[i] != pToken[j])
                    {
                        goto next;
                    }
                }

                if (i == length || pValue[i] == delimiter)
                {
                    return true;
                }

            next:
                i = value.IndexOf(delimiter, i) + 1;
                if (i == 0)
                {
                    break;
                }
            }

            return false;
        }

        public static unsafe bool ContainsTokenHellBrick(string value, string token, char delimiter = ';')
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (string.IsNullOrEmpty(value)) return false;

            int length = value.Length;
            int tokenLength = token.Length;

            int currentTokenOffset = 0;
            int maxTokenOffset = length - tokenLength;

            while (currentTokenOffset <= maxTokenOffset)
            {
                int nextDelimiterOffset = value.IndexOf(delimiter, currentTokenOffset);
                if (nextDelimiterOffset < 0)
                    nextDelimiterOffset = length;

                if (tokenLength == nextDelimiterOffset - currentTokenOffset)
                {
                    for (int i = 0; i < tokenLength; i++)
                    {
                        if (value[currentTokenOffset + i] != token[i])
                            goto next;
                    }

                    return true;
                }

            next:
                currentTokenOffset = nextDelimiterOffset + 1;
            }

            return false;
        }

        public static bool ContainsTokenMonty(string value, string token, char delimiter = ';')
        {
            // this code REQUIRES little endian architecture
            //   it will not function correctly on big endian

            // this code assumes you're running x64
            //   it _works_ regardless, but some ifdef junk for x32
            //   would be handy if you care about ideal x32 perf

            const int charsPerLong = sizeof(long) / sizeof(char);   // 4
            const int charsPerInt = sizeof(int) / sizeof(char);     // 2
            const int bytesPerChar = sizeof(char) / sizeof(byte);   // 2

            if (string.IsNullOrEmpty(token)) return false;
            if (string.IsNullOrEmpty(value)) return false;

            var delimiterTwice = (delimiter << 16) | delimiter;

            var valueLength = value.Length;
            var tokenLength = token.Length;

            if (tokenLength > valueLength) return false;

            int tokenLongs;
            bool tokenTrailingInt, tokenTrailingChar;
            {
                var remainingChars = tokenLength;
                tokenLongs = remainingChars / charsPerLong;
                tokenTrailingInt = (tokenLength & 0x02) != 0;
                tokenTrailingChar = (tokenLength & 0x01) != 0;
            }

            var tokenByteLength = tokenLength * bytesPerChar;

            unsafe
            {
                fixed (char* valuePtr = value, tokenPtr = token)
                {
                    var curValuePtr = valuePtr;
                    var endValuePtr = valuePtr + valueLength;

                    while (true)
                    {
                        // 8-byte steps
                        long* tokenLongPtr = (long*)tokenPtr;
                        {
                            for (var i = 0; i < tokenLongs; i++)
                            {
                                var tokenLong = *tokenLongPtr;

                                var valueLong = *((long*)curValuePtr);

                                if (tokenLong == valueLong)
                                {
                                    // we have a match, continue
                                    tokenLongPtr++;
                                    curValuePtr += charsPerLong;
                                    continue;
                                }
                                else
                                {
                                    goto advanceToNextDelimiter;
                                }
                            }
                        }

                        // can only be 1 4-byte value, 'cause if there were 2 it'd have been handled in the 8-byte stride
                        int* tokenIntPtr = (int*)tokenLongPtr;
                        if (tokenTrailingInt)
                        {
                            var tokenInt = *tokenIntPtr;

                            var valueInt = *((int*)curValuePtr);

                            if (tokenInt == valueInt)
                            {
                                // we have a match, continue
                                tokenIntPtr++;
                                curValuePtr += charsPerInt;
                            }
                            else
                            {
                                goto advanceToNextDelimiter;
                            }
                        }

                        // likewise, only 1 2-byte value or it'd be handled in the 4-byte check
                        char* tokenCharPtr = (char*)tokenIntPtr;
                        if (tokenTrailingChar)
                        {
                            var tokenChar = *tokenCharPtr;

                            var valueChar = *curValuePtr;

                            if (tokenChar == valueChar)
                            {
                                // we have a match, continue
                                tokenCharPtr++;
                                curValuePtr++;
                            }
                            else
                            {
                                goto advanceToNextDelimiter;
                            }
                        }

                        // at this point, the full text of token has been matched, so we need to check if the _next_ char
                        //    in value is a delimiter or the end of value
                        if (curValuePtr == endValuePtr || *curValuePtr == delimiter)
                        {
                            return true;
                        }

                    // in this case, we found a string that starts with value, but is not value
                    //    "Foo" in "FooBar" for example

                    // advance here to skip the rest of whatever's before
                    //   the next delimiter
                    advanceToNextDelimiter:

                        // Really dirty trick time
                        // 
                        // C# strings are length prefixed _and_ null terminated (see: https://msdn.microsoft.com/en-us/library/aa664784(v=vs.71).aspx)
                        //   with '\0' (that is a 2-byte char) so we can read 1 past the "end" of the string w/o issue.
                        // This lets us process an int at a time (2 chars), so if we precalc a mask and do some fancy
                        //   bit twiddling we can avoid a branch per-char.
                        while (true)
                        {
                            var curVal = *((int*)curValuePtr);

                            var masked = curVal ^ delimiterTwice;   // whole char chunks will be zero if any equal delimiter
                            var temp = masked & 0x7FFF7FFF;         // zero out the high bits of both chars
                            temp = temp + 0x7FFF7FFF;               // will overflow _into_ the high bits of any other bits are set in either value
                            temp = (int)(temp & 0x80008000);        // high bits of each char will be set if any of the values _except the high bit_ was non-zero
                            temp = temp | masked;                   // now the high bit will be set if it was set in masked
                            temp = temp | 0x7FFF7FFF;               // whole char will be ones if any of the bits in masked were
                            temp = ~temp;                           // if both chars aren't matches, temp will be set entirely; flip so it'll be zero in that case
                            var neitherMatch = temp == 0;

                            if (neitherMatch)
                            {
                                curValuePtr += charsPerInt;
                                if (curValuePtr >= endValuePtr)
                                {
                                    return false;
                                }
                                continue;
                            }

                            var top16 = temp & 0xFFFF0000;
                            if (top16 != 0)
                            {
                                // got one, and it's in the top of the int under C# rules (big endian),
                                //     but because we're little endian it's actually _at_
                                //     the tail end of the word (so advance two)
                                curValuePtr += charsPerInt;

                                break;
                            }

                            var bottom16 = temp & 0x0000FFFF;
                            if (bottom16 != 0)
                            {
                                // got one, and it's at the bottom of the int under C# rules (big endian),
                                //     but because we're little endian it's actually _at_
                                //     the head of the word (so just advance one)
                                curValuePtr += 1;
                            }
                        }

                        // we only need to do this check after a delimiter is advanced
                        //    because this check _itself_ guarantees that we won't
                        //    exceed the end of value before we're done comparing
                        //    against token
                        var remainingBytesInValue = ((byte*)endValuePtr) - ((byte*)curValuePtr);    // we do this bytes to save an op
                        if (remainingBytesInValue < tokenByteLength)
                        {
                            // not possible for token to fit in string, fail early
                            return false;
                        }
                    }
                }
            }
        }

        public static bool ContainsTokenSingleScan(string value, string token, char delimiter = ';')
        {
            if (string.IsNullOrEmpty(token)) return false;
            if (string.IsNullOrEmpty(value)) return false;

            var inTokenScanMode = true; // as we see the start as the char right after a delimiter
            int indexInToken = 0;
            for (int i = 0; i < value.Length; i++)
            {
                if (inTokenScanMode)
                {
                    if (indexInToken < token.Length)
                    {
                        if (value[i] == token[indexInToken])
                        {
                            indexInToken++;
                        }
                        else
                        {
                            inTokenScanMode = false;
                            indexInToken = 0;
                        }
                    }
                    else
                    {
                        if (value[i] == delimiter)
                        {
                            return true;
                        }
                        inTokenScanMode = false;
                        indexInToken = 0;
                    }
                }
                else
                {
                    inTokenScanMode = (value[i] == delimiter);
                }
            }
            return inTokenScanMode && (indexInToken == token.Length);
        }
    }
}
