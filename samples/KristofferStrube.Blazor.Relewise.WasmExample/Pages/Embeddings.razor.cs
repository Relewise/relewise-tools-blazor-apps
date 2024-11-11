namespace KristofferStrube.Blazor.Relewise.WasmExample.Pages
{
    public partial class Embeddings
    {
        string chorus = "";
        string[] tokens = [];
        Dictionary<string, PredictionCollection> tokenPredictions = [];
        Dictionary<string, double[]> tokenBagOfWordEmbeddings = [];
        Dictionary<string, double> squaredSums = [];

        int contextWindow = 5;

        public void MakeEmbedding()
        {
            tokenPredictions.Clear();

            tokens = GetTokens(chorus);

            Dictionary<string, List<Observation>> observations = [];

            for (int i = 0; i < tokens.Length; i++)
            {
                string centerWord = tokens[i];

                if (!observations.TryGetValue(centerWord, out var observationsForWord))
                {
                    observationsForWord = [];
                    observations[centerWord] = observationsForWord;
                }

                for (int j = Math.Max(i - contextWindow, 0); j < tokens.Length && j <= i + contextWindow; j++)
                {
                    if (i == j)
                        continue;

                    observationsForWord.Add(new(j - i, tokens[j]));
                }
            }

            foreach ((string centerToken, var observationsForWord) in observations)
            {
                Dictionary<int, Dictionary<string, int>> observationsPerOffset = [];
                Dictionary<int, int> numberOfObservationsPerOffset = [];
                Dictionary<string, int> tokenCounts = [];

                foreach (var observation in observationsForWord)
                {
                    if (!observationsPerOffset.TryGetValue(observation.Offset, out var observationForOffset))
                    {
                        observationForOffset = [];
                        observationsPerOffset[observation.Offset] = observationForOffset;
                    }

                    if (!observationForOffset.TryGetValue(observation.Token, out int count))
                    {
                        observationForOffset[observation.Token] = 1;
                    }
                    else
                    {
                        observationForOffset[observation.Token] = count + 1;
                    }

                    if (!numberOfObservationsPerOffset.TryGetValue(observation.Offset, out int numberOfObservations))
                    {
                        numberOfObservationsPerOffset[observation.Offset] = 1;
                    }
                    else
                    {
                        numberOfObservationsPerOffset[observation.Offset] = numberOfObservations + 1;
                    }

                    if (!tokenCounts.TryGetValue(observation.Token, out int tokenCount))
                    {
                        tokenCounts[observation.Token] = 1;
                    }
                    else
                    {
                        tokenCounts[observation.Token] = tokenCount + 1;
                    }
                }

                List<Prediction> predictions = [];
                foreach ((int offset, Dictionary<string, int> counts) in observationsPerOffset)
                {
                    foreach ((string predictionToken, int count) in counts)
                    {
                        predictions.Add(new(offset, predictionToken, count / (float)numberOfObservationsPerOffset[offset]));
                    }
                }
                tokenPredictions[centerToken] = new(predictions.ToArray(), observationsForWord.Count);
                tokenBagOfWordEmbeddings[centerToken] = tokens.Select(t => tokenCounts.TryGetValue(t, out int count) ? count / (double)observationsForWord.Count : 0).ToArray();
            }

            Console.WriteLine($"Beginning to calcualate squared sums for {tokens.Length} tokens");
            StateHasChanged();

            int c = 0;
            foreach (string token in tokens)
            {
                squaredSums[token] = Enumerable.Range(0, tokens.Length).Sum(i => tokenBagOfWordEmbeddings[token][i] * tokenBagOfWordEmbeddings[token][i]);
                Console.WriteLine($"Done with {++c}/{tokens.Length}");
            }

            Console.WriteLine($"Done calcualating squared sums for {tokens.Length} tokens");
            StateHasChanged();
        }

        public string[] GetTokens(string input)
        {
            return input
                .ToLower()
                .Split([' ', '-', '_', '.', ',', ':', ';', '\\', '/', '\'', '\n', '\r', '|', '´', '`', '"', '(', ')', '+', '[', ']', '{', '}', '?', '!', '#', '@'])
                .Where(w => w.Length > 0)
                .ToArray();
        }

        public string CreateSentence(string token, int length)
        {
            if (length <= 1)
            {
                return token;
            }
            else if (tokenPredictions.TryGetValue(token, out var predictionCollection))
            {
                var predictionsForNextToken = predictionCollection.Predictions.Where(p => p.Offset == 1);
                if (predictionsForNextToken.Count() is 0)
                    return token;

                float choice = (float)Random.Shared.NextDouble();
                double chanceConsumed = 0;
                Prediction prediction = default;
                foreach (var nextPrediction in predictionsForNextToken)
                {
                    prediction = nextPrediction;
                    chanceConsumed += nextPrediction.Confidence;
                    if (chanceConsumed > choice)
                        break;
                }

                var nextPart = CreateSentence(prediction.Token, length - 1);
                return $"{token} {nextPart}";
            }
            else
            {
                return token;
            }
        }

        public string ClosestToken(string token)
        {
            string? tokenWithGreatestSimilarity = null;

            double greatestSimilarity = double.MinValue;

            double[] primaryEmbeddings = tokenBagOfWordEmbeddings[token];

            foreach ((string secondToken, double[] secondEmbeddings) in tokenBagOfWordEmbeddings)
            {
                if (secondToken == token)
                    continue;

                var similarity = ConsineSimilarity(primaryEmbeddings, secondEmbeddings, squaredSums[token], squaredSums[secondToken]);
                if (similarity > greatestSimilarity)
                {
                    greatestSimilarity = similarity;
                    tokenWithGreatestSimilarity = secondToken;
                }
            }

            Console.WriteLine($"{token} -> {tokenWithGreatestSimilarity}");

            return tokenWithGreatestSimilarity!;
        }

        public double ConsineSimilarity(double[] a, double[] b, double aSquaredSum, double bSquaredSum)
        {
            var dotProduct = Enumerable.Range(0, tokens.Length).Sum(i => a[i] * b[i]);
            return dotProduct / (Math.Sqrt(aSquaredSum) * Math.Sqrt(bSquaredSum));
        }

        public readonly record struct Observation(int Offset, string Token);

        public readonly record struct PredictionCollection(Prediction[] Predictions, int Observations);

        public readonly record struct Prediction(int Offset, string Token, float Confidence);

        public readonly record struct Embedding(string Token, float Confidence);
    }
}