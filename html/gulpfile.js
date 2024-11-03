/// <binding BeforeBuild='less' />
// Above line allows Visual Studio to trigger the 'less' task before building the project.

// Automate transpilation of .less files, see https://code.visualstudio.com/Docs/languages/CSS#_automating-sassless-compilation
var gulp = require('gulp');
var less = require('gulp-less');
var fs = require('fs');

gulp.task('less', function (done) {
    gulp
        .src('styles.less') // source file(s) to process
        .pipe(less()) // pass them through the LESS compiler
        .pipe(gulp.dest(f => f.base)); // Use the base directory of the source file for output

    done(); // signal task completion
});

gulp.task('fill-template-html', function (done) {
    // Read and parse model.json
    fs.readFile('model.json', 'utf8', function (err, data) {
        if (err) {
            console.error('Error reading model.json:', err);
            done(err);
            return;
        }

        const model = JSON.parse(data); // Parse the JSON data

        // Read template.html
        fs.readFile('template.html', 'utf8', function (err, templateContent) {
            if (err) {
                console.error('Error reading template.html:', err);
                done(err);
                return;
            }

            // Replace placeholders in template with values from model
            let outputContent = templateContent;

            for (const [key, value] of Object.entries(model)) {
                const placeholder = `{{${key}}}`; // Create the placeholder
                outputContent = outputContent.replace(new RegExp(placeholder, 'g'), value); // Replace all occurrences
            }

            // Save the replaced content
            fs.writeFile('class-diagrammer.html', outputContent, 'utf8', function (err) {
                if (err) {
                    console.error('Error writing class-diagrammer.html:', err);
                    done(err);
                    return;
                }

                console.log('class-diagrammer.html generated successfully.');
                done(); // Signal completion
            });
        });
    });
});

// the default task that runs when Gulp is executed without any specific task name
gulp.task(
    'default',
    // Run the 'less' task first, then start watching for changes
    gulp.series('less', function (done) {
        gulp.watch('*.less', gulp.series('less'));  // Watch for any changes in .less files and rerun the 'less' task
        done(); // signal task completion
    })
);