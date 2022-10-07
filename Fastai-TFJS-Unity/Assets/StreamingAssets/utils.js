// Perform inference with the provided model and input data
async function PerformInferenceAsync(model, float32Data, shape) {

    const outputData = tf.tidy(() => {
        // Initialize the input tensor
        const input_tensor = tf.tensor(float32Data, shape, 'float32');
        // Make a prediction.
        return model.predict(input_tensor);
    });
    // Pass raw output through a SoftMax function
    let results = await outputData.data();
    // Extract the predicted class from the model output
    let index = await tf.argMax(results).data();
    return [index, results[index]];
}