import subprocess
import xml.etree.ElementTree as et
import datetime
import json
import os.path
import os

def IncrementVersionString(verstr):
	verstr = verstr.split('.')

	major = int(verstr[0])
	minor = int(verstr[1])
	build = int(verstr[2])
	rev = int(verstr[3])

	dt = datetime.date.today()
	# The version is split into two 16-bit fields.
	# major.minor.YMM.DDRRR
	build = (dt.year - 2016) * 100 + dt.month
	rev_rrr = rev - (rev // 1000 * 1000)
	rev = dt.day * 1000 + (rev_rrr + 1)

	return '%s.%s.%s.%s' % (major, minor, build, rev)

def IncrementVersionOnProject(breaking_changes=False):
	buildinfo_path = 'buildinfo.json'
	if os.path.exists(buildinfo_path):
		fd = open(buildinfo_path, 'r')
		buildinfo = json.loads(fd.read())
		fd.close()
	else:
		buildinfo = {
			'version': '0.0.0.0',
		}

	buildinfo['version'] = IncrementVersionString(buildinfo['version'])

	fd = open(buildinfo_path, 'w')
	fd.write(json.dumps(buildinfo))
	fd.close()

print('+ incrementing version')
IncrementVersionOnProject()

def jsx_to_js(infile, outputs):
	print('+ compiling JSX into JS for %s' % infile)
	stdout, stderr = subprocess.Popen([
			'babel',
			'--plugins',
			'transform-react-jsx',
			infile,
	], stdout=subprocess.PIPE, stderr=subprocess.PIPE).communicate('')

	stderr = stderr.decode('utf8')
	stdout = stdout.decode('utf8')

	if len(stderr) > 0:
		print(stderr)
		raise Exception('jsx_to_js failed')

	for (outfile, outfilemode) in outputs:
		print(' - writing %s' % outfile)
		fd = open(outfile, outfilemode)
		fd.write('/// <jsx-source-file>%s</jsx-source-file>\n' % infile)
		fd.write(stdout)
		fd.close()

def compile_jsx_and_concat(pdir):
	nodes = os.listdir(pdir)

	open(os.path.join(pdir, 'app.js'), 'w').close()

	for node in nodes:
		(node_base, ext) = os.path.splitext(node)

		if ext != '.jsx':
			continue
		
		if node_base == 'app':
			continue
		
		jsx_to_js(
			os.path.join(pdir, node), 
			[
				(os.path.join(pdir, 'app.js'), 'a'),
				(os.path.join(pdir, '%s.js' % node_base), 'w'),
			]
		)
	
	if os.path.exists(os.path.join(pdir, 'app.jsx')):
		jsx_to_js(
			os.path.join(pdir, 'app.jsx'), 
			[
				(os.path.join(pdir, 'app.js'), 'a'),
			]
		)	

compile_jsx_and_concat('./webres')