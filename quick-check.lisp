;; requires prelude.lisp
;; requires list.lisp
;; requires pair.lisp

(func mk-gen (seed)
  (pair 'gen seed))

(setq gen-seed snd)

(func next-gen (g)
  (mk-gen (modulo (plus 17 (times 7919 (gen-seed g))) 4271)))

(func rnd-integer (i j)
  (lambda (g)
    (pair
      (plus i (modulo (gen-seed g) (minus j i)))
      (next-gen g)
    )))

'(random integer between 10 and 34)
(fst ((rnd-integer 10 34) (mk-gen 123)))

(func gen-map (f)
  (lambda (ma) (lambda (g) (prog ()
    (setq mag (ma g))
    (setq a  (fst mag))
    (setq g1 (snd mag))
    (pair (f a) g1)))))

(func generate (n f g)
  (cond (equal 0 n)
    '()
    (prog (fg x g1)
      (setq fg (f g))
      (setq x  (fst fg))
      (setq g1 (snd fg))
      (cons x (generate (minus n 1) f g1)))))

(setq rnd-bool
  ((gen-map even?) (rnd-integer 1 12345)))

'(10 random bools)
(generate 10 rnd-bool (mk-gen 13123))

(func gen-pure (x)
  (lambda (g) (pair x g)))

(func gen-bind (ma f)
  (lambda (g) (prog ()
    (setq mag (ma g))
    (setq a  (fst mag))
    (setq g1 (snd mag))
    ((f a) g1))))

(func rnd-element (lst)
  (gen-bind (rnd-integer 0 (times 2 (length lst))) (lambda (m)
  (cond (equal 0 (modulo m (length lst)))
    (gen-pure (head lst))
    (rnd-element (tail lst))))))
'(10 random elements of (1 2 3))
(generate 10 (rnd-element '(1 2 3)) (mk-gen 13))

(func gen-lift2 (f)
  (lambda (ma mb)
    (gen-bind ma (lambda (a)
    ((gen-map ((curry f) a)) mb)))))

(setq rnd-pair
  (gen-lift2 pair))
'(5 random pairs of bools)
(generate 5 (rnd-pair rnd-bool rnd-bool) (mk-gen 31337))

(func rnd-list (f n)
  (cond (equal 0 n)
    (gen-pure '())
    ((gen-lift2 cons) f (rnd-list f (minus n 1)))))
'(random list of integers from 1 to 10 with 10 elements)
(fst ((rnd-list (rnd-integer 1 10) 10) (mk-gen 123)))

(func property (gen pred)
  (cons 'property (cons gen (cons pred '()))))

(setq property-gen  (compose head tail))
(setq property-pred (compose head (compose tail tail)))

(func counterexamples (prop g)
  (prog ()
    (setq f    (property-gen  prop))
    (setq pred (property-pred prop))
    (filter (compose not pred) (generate 100 f g))))

'(counterexamples for property that all numbers are greater than 5 in range from 1 to 100)
(counterexamples
  (property (rnd-integer 1 100)
            (lambda (x) (greater x 5)))
  (mk-gen 123))
'(counterexamples for property that all numbers are greater than 0 in range from 1 to 100)
(counterexamples
  (property (rnd-integer 1 100)
            (lambda (x) (greater x 0)))
  (mk-gen 123))

(func check (prop g)
  (prog ()
    (setq cexamples (counterexamples prop g))
    (cond (null? cexamples)
        '(Success)
        (pair '(Falsified! Counterexample is) (head cexamples)))))
(check (property (rnd-integer 1 10) (lambda (n) (less n 11))) (mk-gen 123))
(check (property (rnd-integer 1 10) (lambda (n) (less n 5))) (mk-gen 123))

(setq rnd-2D-point
  (rnd-pair
    (rnd-integer -99 99)
    (rnd-integer -99 99)))
'(5 random 2D points)
(generate 5 rnd-2D-point (mk-gen 123))

(func euclidean-distance-2D (p1 p2)
  (prog ()
    (setq x1 (fst p1))
    (setq y1 (snd p1))
    (setq x2 (fst p2))
    (setq y2 (snd p2))
    (sqrt (plus (sqr (minus x2 x1)) (sqr (minus y2 y1))))))
'(euclidean-distance between (3 4) and (0 0))
(euclidean-distance-2D (pair 3 4) (pair 0 0))

(func triangular (gen mu)
  (prog ()
    (setq g
      (gen-bind gen (lambda (p1)
      (gen-bind gen (lambda (p2)
      (gen-bind gen (lambda (p3)
      (gen-pure (cons p1 (cons p2 (cons p3 '())))))))))))
    (func pred (ps)
      (prog ()
        (setq p1 (head ps))
        (setq p2 (head (tail ps)))
        (setq p3 (head (tail (tail ps))))
        (greatereq (plus (mu p1 p2) (mu p2 p3)) (mu p1 p3))))
    (property g pred)))

'(Check if triangular property holds for euclidean-distance-2D)
(check (triangular rnd-2D-point euclidean-distance-2D) (mk-gen 1234))

(func bad-distance-2D (p1 p2)
  (prog ()
    (setq x1 (fst p1))
    (setq x2 (snd p1))
    (setq y1 (fst p2))
    (setq y2 (snd p2))
    (sum (cons x1 (cons x2 (cons y1 (cons y2 '())))))))
'(Check if triangular property holds for bad-distance-2D)
(check (triangular rnd-2D-point bad-distance-2D) (mk-gen 21345))

(func functor-law-1 (map)
  (prog ()
    (setq g
      (gen-bind (rnd-integer 0 100) (lambda (n)
      (rnd-list (rnd-integer -99 99) n))))
    (func pred (lst)
      (equal (map id lst) lst))
    (property g pred)))

'(Check if list map holds functor-law-1)
(check (functor-law-1 map) (mk-gen 234))

'(Check if functor-law-1 holds for bad-map)
(func bad-map (f xs)
  (reverse xs))
(check (functor-law-1 bad-map) (mk-gen 2344))

(setq rnd-int->int
  (gen-bind (rnd-integer -100 100) (lambda (m)
  (rnd-element
    (cons id
    (cons (const m)
    (cons ((curry plus) m)
    (cons ((curry times) m)
    '()))))))))

(func functor-law-2 (map)
  (prog ()
    (setq g
      (gen-bind (rnd-integer 0 10) (lambda (n)
      (gen-bind rnd-int->int (lambda (f)
      (gen-bind rnd-int->int (lambda (g)
      (gen-bind (rnd-list (rnd-integer -5 5) n) (lambda (lst)
      (gen-pure (cons f (cons g (cons lst '())))))))))))))
    (func pred (f-g-lst)
      (prog ()
        (setq f   (head f-g-lst))
        (setq g   (head (tail f-g-lst)))
        (setq lst (head (tail (tail f-g-lst))))
        (equal (map f (map g lst)) (map (compose f g) lst))))
    (property g pred)))

'(Check if functor-law-2 holds for list map)
(check (functor-law-2 map) (mk-gen 1234))

'(Check if functor-law-2 holds for bad-map)
(check (functor-law-2 bad-map) (mk-gen 7098))
