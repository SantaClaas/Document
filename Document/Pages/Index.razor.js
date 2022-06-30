export async function loadDocument(source) {
    const task = pdfjsLib.getDocument(source);
    return await task.promise;
}

export function getProperty(property, reference){
   return reference[property];
}

export function setProperty(property, value, reference){
    return reference[property] = value;
}

export function createPath2d(){
    return new Path2D();
}


// To convert an element reference to a JS object reference in .NET. In other languages/frameworks a function that
// returns the same object is called identity/id
export function identity(reference) { return reference;}