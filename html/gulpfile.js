/// <binding BeforeBuild='less' />
// Above line allows Visual Studio to trigger the 'less' task before building the project.

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

/*  Run individual build tasks first, then start watching for changes
    see https://code.visualstudio.com/Docs/languages/CSS#_automating-sassless-compilation */
gulp.task(
    'rebuild-on-change',
        // Watch for changes in source files and rerun the corresponding build task
        gulp.watch('styles.less', gulp.series('less'));
        gulp.watch(['template.html', 'model.json'], gulp.series('fill-template-html'));
        done(); // signal task completion
    })
);