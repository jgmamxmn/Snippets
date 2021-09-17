using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.FastTree;

public class JOMLTrain
{
	internal static void DoMLTrain(List<JObject> JObjects)
	{
		string TargetColumn = "cat";

		// Load data
		var jodv = new MxmnAI.JODataView(JObjects, TargetColumn);
		IDataView dv = jodv;
		var mlContext = new MLContext();

		// Split data
		DataOperationsCatalog.TrainTestData dataSplit = mlContext.Data.TrainTestSplit(dv, testFraction: 0.01);
		IDataView trainData = dataSplit.TrainSet;
		IDataView testData = dataSplit.TestSet;

		// Normalize stuff
		foreach (var KVP in jodv.Transformers)
		{
			// Insert any normalizations here...
			dv = KVP.Value(KVP.Key, mlContext, dv);
		}

		// Prep features
		var dataPrepEstimator = mlContext.Transforms.Concatenate("Features", jodv.FeaturableColumns.ToArray())
			.Append(mlContext.Transforms.Text.FeaturizeText("Features"))
			.Append(mlContext.Transforms.NormalizeMinMax("Features"));
		var dataPrepTransformer = dataPrepEstimator.Fit(trainData);
		trainData = dataPrepTransformer.Transform(trainData);

		// Train
		string labelCol = TargetColumn;
		var trainer = // Define your trainer, e.g.:
			mlContext.Transforms.Conversion.MapValueToKey(TargetColumn, TargetColumn)
			.Append(
			mlContext.MulticlassClassification.Trainers.LightGbm
			(
					labelColumnName: TargetColumn,
					featureColumnName: "Features",
					numberOfIterations: 400,
					numberOfLeaves: 80,
					minimumExampleCountPerLeaf: 4
			))
			.Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel", "PredictedLabel"));
		var trainingPipeline = dataPrepEstimator.Append(trainer);
		var trainedModel = trainingPipeline.Fit(trainData);

		// Test - TODO
		// Save - TODO

		}

	}

}
